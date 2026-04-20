#include "types.h"
#include "alvium_camera.h"
#include "mwir_camera.h"
#include "compositor.h"
#include "udp_listener.h"
#include "trc_a1.hpp"

#include <gst/gst.h>
#include <gst/app/gstappsrc.h>

#include <iostream>
#include <string>
#include <csignal>
#include <unistd.h>
#include <thread>
#include <chrono>
#include <fstream>
#include <sstream>

// ============================================================================
// VERSION
// ============================================================================
static constexpr uint32_t TRC_VERSION = VERSION_PACK(4, 1, 1);

// ============================================================================
// Signal handling
// ============================================================================

static GlobalState g_state;
static int g_signalPipe[2] = {-1, -1};

static void signalHandler(int sig) {
    g_state.running.store(false);
    if (g_signalPipe[1] >= 0) {
        if (write(g_signalPipe[1], "x", 1) < 0) {}
    }
}

static void installSignalHandlers() {
    if (pipe(g_signalPipe) < 0) {
        std::cerr << "[Signal] Failed to create signal pipe" << std::endl;
    }
    struct sigaction sa{};
    sa.sa_handler = signalHandler;
    sigemptyset(&sa.sa_mask);
    sa.sa_flags = 0;
    sigaction(SIGINT, &sa, nullptr);
    sigaction(SIGTERM, &sa, nullptr);
}

// ============================================================================
// Jetson stats reader (runs in stats thread)
// ============================================================================

static CpuSnapshot g_cpuSnapshot;

static int readJetsonTemp() {
    std::ifstream f("/sys/devices/virtual/thermal/thermal_zone0/temp");
    int temp = 0;
    if (f >> temp) temp /= 1000;  // millidegrees → degrees
    return temp;
}

static int readJetsonCpuLoad(CpuSnapshot& snapshot) {
    std::ifstream f("/proc/stat");
    std::string line;
    if (!std::getline(f, line)) return 0;

    std::istringstream iss(line);
    std::string cpu;
    iss >> cpu;

    uint64_t user, nice, system, idle, iowait, irq, softirq, steal;
    iss >> user >> nice >> system >> idle >> iowait >> irq >> softirq >> steal;

    uint64_t totalIdle = idle + iowait;
    uint64_t totalAll = user + nice + system + idle + iowait + irq + softirq + steal;

    uint64_t deltaIdle = totalIdle - snapshot.totalIdle;
    uint64_t deltaAll = totalAll - snapshot.totalAll;

    snapshot.totalIdle = totalIdle;
    snapshot.totalAll = totalAll;

    if (deltaAll == 0) return 0;
    return (int)(100.0 * (1.0 - (double)deltaIdle / (double)deltaAll));
}

static int readJetsonGpuLoad() {
    // Tegra sysfs GPU load — 0-1000, divide by 10 for %.
    // Path confirmed on JetPack 6.2.2 / Orin NX.
    std::ifstream f("/sys/devices/platform/gpu.0/load");
    int load = 0;
    if (f >> load) load /= 10;
    return load;
}

static void statsThreadFunc(Compositor* compositor, AlviumCamera* alvium,
                            MwirCamera* mwir, GlobalState& state) {
    std::cerr << "[Stats] Thread started" << std::endl;
    int cycle = 0;

    // Complementary filter state for CPU and GPU load.
    // alpha=0.3: ~3s time constant at 1s poll — responsive but not jittery.
    static constexpr float LOAD_ALPHA = 0.3f;
    float cpuFiltered = 0.0f;
    float gpuFiltered = 0.0f;

    while (state.running.load()) {
        std::this_thread::sleep_for(std::chrono::seconds(1));
        if (!state.running.load()) break;

        // Current values — every 1s
        alvium->refreshOsdValues();
        mwir->refreshOsdValues();   // no-op until MWIR SDK available

        // CPU + GPU load — every 1s with complementary filter
        cpuFiltered = LOAD_ALPHA * (float)readJetsonCpuLoad(g_cpuSnapshot)
                    + (1.0f - LOAD_ALPHA) * cpuFiltered;
        gpuFiltered = LOAD_ALPHA * (float)readJetsonGpuLoad()
                    + (1.0f - LOAD_ALPHA) * gpuFiltered;
        compositor->jetsonCpuLoad.store((int)(cpuFiltered + 0.5f));
        compositor->jetsonGpuLoad.store((int)(gpuFiltered + 0.5f));

        // Ranges — every 5s
        if (++cycle % 5 == 0) {
            alvium->refreshOsdRanges();
            mwir->refreshOsdRanges();   // no-op until MWIR SDK available
        }

        // Temperature — every 30s
        if (cycle % 30 == 0) {
            compositor->jetsonTemp.store(readJetsonTemp());
        }
    }

    std::cerr << "[Stats] Thread stopped" << std::endl;
}

// ============================================================================
// GStreamer H.264 encode pipeline builder
// ============================================================================

// Returns true if host is in the multicast range 224.0.0.0 – 239.255.255.255
static bool isMulticastAddr(const std::string& host) {
    try {
        int first = std::stoi(host.substr(0, host.find('.')));
        return first >= 224 && first <= 239;
    } catch (...) {
        return false;
    }
}

struct EncodePipeline {
    GstElement* pipeline{nullptr};
    GstElement* encoder{nullptr};
    GstAppSrc*  appsrc{nullptr};
    std::atomic<int> bitrateMbps_{10};  // current bitrate for OSD/telemetry

    bool build(const std::string& destHost, int videoPort, int bitrateMbps = 10) {
        bitrateMbps_.store(bitrateMbps);
        bool multicast = isMulticastAddr(destHost);

        std::ostringstream pipeStr;
        pipeStr << "appsrc name=enc_src is-live=true format=time"
                << " ! queue max-size-buffers=2 leaky=downstream"
                << " ! videoconvert"
                << " ! video/x-raw,format=BGRx"
                << " ! nvvidconv"
                << " ! video/x-raw(memory:NVMM),format=NV12"
                << " ! nvv4l2h264enc name=h264enc"
                <<   " bitrate=" << (bitrateMbps * 1000000)
                <<   " profile=2"
                <<   " preset-level=2"
                <<   " control-rate=1"
                <<   " maxperf-enable=true"
                <<   " poc-type=2"
                <<   " num-B-Frames=0"
                <<   " qp-range=\"10,36:10,36:10,36\""
                <<   " insert-sps-pps=true"
                <<   " insert-vui=true"
                <<   " idrinterval=60"
                << " ! h264parse"
                << " ! rtph264pay config-interval=1 pt=96";

        if (multicast) {
            pipeStr << " ! multiudpsink clients=" << destHost << ":" << videoPort
                    <<   " sync=false async=false";
        } else {
            pipeStr << " ! udpsink host=" << destHost
                    <<   " port=" << videoPort
                    <<   " sync=false async=false";
        }

        std::cerr << "\n=== GStreamer Encode Pipeline ===" << std::endl;
        std::cerr << pipeStr.str() << std::endl;

        GError* error = nullptr;
        pipeline = gst_parse_launch(pipeStr.str().c_str(), &error);
        if (error) {
            std::cerr << "[Encode] Pipeline parse error: " << error->message << std::endl;
            g_error_free(error);
            return false;
        }

        GstElement* src = gst_bin_get_by_name(GST_BIN(pipeline), "enc_src");
        if (!src) {
            std::cerr << "[Encode] Failed to get appsrc element" << std::endl;
            gst_object_unref(pipeline);
            pipeline = nullptr;
            return false;
        }
        appsrc = GST_APP_SRC(src);

        // Get named encoder element for runtime property changes
        encoder = gst_bin_get_by_name(GST_BIN(pipeline), "h264enc");
        if (!encoder) {
            std::cerr << "[Encode] WARNING: Could not get encoder element for runtime control" << std::endl;
        }

        // Configure appsrc caps
        GstCaps* caps = gst_caps_new_simple("video/x-raw",
            "format", G_TYPE_STRING, "BGR",
            "width", G_TYPE_INT, Defaults::FRAME_WIDTH,
            "height", G_TYPE_INT, Defaults::FRAME_HEIGHT,
            "framerate", GST_TYPE_FRACTION, Defaults::FRAME_RATE, 1,
            nullptr);
        gst_app_src_set_caps(appsrc, caps);
        gst_caps_unref(caps);

        gst_app_src_set_stream_type(appsrc, GST_APP_STREAM_TYPE_STREAM);

        std::cerr << "=== UDP Streaming ===" << std::endl;
        std::cerr << "Mode:    " << (multicast ? "MULTICAST" : "UNICAST") << std::endl;
        std::cerr << "Destination: " << destHost << ":" << videoPort << std::endl;
        std::cerr << "Bitrate: " << bitrateMbps << " Mbps (CBR)" << std::endl;
        std::cerr << "Encoder: nvv4l2h264enc profile=High preset=Fast poc-type=2 maxperf=on" << std::endl;
        std::cerr << "QP range: I[10,36] P[10,36] B[10,36]" << std::endl;
        std::cerr << "IDR interval: 60 frames" << std::endl;

        return true;
    }

    // Runtime bitrate change (Mbps). Returns true on success.
    bool setBitrate(int mbps) {
        if (!encoder || mbps < 1 || mbps > 50) return false;
        g_object_set(G_OBJECT(encoder), "bitrate", (guint)(mbps * 1000000), nullptr);
        bitrateMbps_.store(mbps);
        std::cerr << "[Encode] Bitrate set to " << mbps << " Mbps" << std::endl;
        return true;
    }

    int getBitrate() const { return bitrateMbps_.load(); }

    bool start() {
        if (!pipeline) return false;
        GstStateChangeReturn ret = gst_element_set_state(pipeline, GST_STATE_PLAYING);
        return (ret != GST_STATE_CHANGE_FAILURE);
    }

    void stop() {
        if (pipeline) {
            gst_element_set_state(pipeline, GST_STATE_NULL);
            if (encoder) { gst_object_unref(encoder); encoder = nullptr; }
            gst_object_unref(pipeline);
            pipeline = nullptr;
            appsrc = nullptr;
        }
    }
};

// ============================================================================
// Command-line argument parsing
// ============================================================================

struct Args {
    std::string destHost = "192.168.1.1";
    std::string mwirDevice = Defaults::MWIR_DEVICE;
    int  asciiPort = Defaults::UDP_ASCII_PORT;
    int  binaryPort = Defaults::A2_PORT;        // TRC-M8: ICD v1.7 A2 port (10018)
    int  videoPort = Defaults::VIDEO_PORT;
    int  bitrateMbps = 10;               // --bitrate: H.264 encoder bitrate (Mbps)
    bool mwirLive = false;            // --mwir-live: start MWIR in live mode
    bool debugMode = false;            // --debug: enable debug logging from boot
    bool osdEnabled = false;            // --osd ON: enable OSD text overlay at launch
    bool focusScoreEnabled = false;            // --focusscore ON: enable focus score at launch
    bool cocoAmbient = false;            // --coco-ambient: load model + enable ambient on boot
    std::string viewMode = "CAM1";           // --view: default compositor view on startup
};

static Args parseArgs(int argc, char* argv[]) {
    Args args;
    for (int i = 1; i < argc; i++) {
        std::string arg = argv[i];
        if (arg == "--dest-host" && i + 1 < argc)         args.destHost = argv[++i];
        else if (arg == "--mwir-device" && i + 1 < argc)   args.mwirDevice = argv[++i];
        else if (arg == "--udp-ascii-port" && i + 1 < argc)  args.asciiPort = std::stoi(argv[++i]);
        else if (arg == "--udp-bin-port" && i + 1 < argc)    args.binaryPort = std::stoi(argv[++i]);
        else if (arg == "--video-port" && i + 1 < argc)      args.videoPort = std::stoi(argv[++i]);
        else if (arg == "--mwir-live")                         args.mwirLive = true;
        else if (arg == "--view" && i + 1 < argc)             args.viewMode = argv[++i];
        else if (arg == "--debug")                             args.debugMode = true;
        else if (arg == "--bitrate" && i + 1 < argc)          args.bitrateMbps = std::stoi(argv[++i]);
        else if (arg == "--osd" && i + 1 < argc) {
            std::string v = argv[++i];
            std::transform(v.begin(), v.end(), v.begin(), ::toupper);
            args.osdEnabled = (v != "OFF");
        }
        else if (arg == "--focusscore" && i + 1 < argc) {
            std::string v = argv[++i];
            std::transform(v.begin(), v.end(), v.begin(), ::toupper);
            args.focusScoreEnabled = (v == "ON");
        }
        else if (arg == "--coco-ambient")
            args.cocoAmbient = true;
        else if (arg == "--usage") {
            std::cout << "Example usage:\n"
                      << "  Multicast: ./trc --dest-host 239.127.1.21 --mwir-live --view PIP\n"
                      << "  Unicast:   ./trc --dest-host 192.168.1.208 --mwir-live --view PIP\n";
            exit(0);
        }
        else if (arg == "--gst-verify") {
            std::cout << "GStreamer receive pipelines (run on THEIA to verify stream):\n"
                      << "\nUnicast:\n"
                      << "  gst-launch-1.0.exe udpsrc port=5000 buffer-size=2097152 "
                      << "caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" "
                      << "! rtpjitterbuffer latency=50 drop-on-latency=true "
                      << "! rtph264depay ! h264parse ! nvh264dec "
                      << "! videoconvert n-threads=4 "
                      << "! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true\n"
                      << "\nMulticast (group 239.127.1.21):\n"
                      << "  gst-launch-1.0.exe udpsrc multicast-group=239.127.1.21 auto-multicast=true "
                      << "port=5000 buffer-size=2097152 "
                      << "caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" "
                      << "! rtpjitterbuffer latency=50 drop-on-latency=true "
                      << "! rtph264depay ! h264parse ! nvh264dec "
                      << "! videoconvert n-threads=4 "
                      << "! fpsdisplaysink sync=false text-overlay=true signal-fps-measurements=true\n";
            exit(0);
        }
        else if (arg == "--version" || arg == "-v") {
            std::cout << "TRC "
                      << VERSION_MAJOR(TRC_VERSION) << "."
                      << VERSION_MINOR(TRC_VERSION) << "."
                      << VERSION_PATCH(TRC_VERSION) << " "
                      << __DATE__ << " " << __TIME__
                      << std::endl;
            exit(0);
        }
        else if (arg == "--help" || arg == "-h") {
            std::cerr << "Usage: " << argv[0] << " [options]\n"
                << "  --dest-host <ip>         Stream destination IP (default: 192.168.1.1)\n"
                << "  --mwir-device <path>     MWIR V4L2 device (default: " << Defaults::MWIR_DEVICE << ")\n"
                << "  --udp-ascii-port <port>  ASCII command port (default: " << Defaults::UDP_ASCII_PORT << ")\n"
                << "  --udp-bin-port <port>    A2 binary command port (default: " << Defaults::A2_PORT << ")\n"
                << "  --video-port <port>      H.264 video output port (default: " << Defaults::VIDEO_PORT << ")\n"
                << "  --bitrate <1-50>         H.264 encoder bitrate Mbps (default: 10)\n"
                << "  --mwir-live              Start MWIR camera in live mode (default: test source)\n"
                << "  --debug                  Enable debug logging from boot (default: off)\n"
                << "  --osd <ON|OFF>           OSD text overlay on startup (default: OFF)\n"
                << "  --focusscore <ON|OFF>    Focus score overlay on startup (default: OFF)\n"
                << "  --coco-ambient           Load COCO model + enable ambient scan on boot (default: off)\n"
                << "  --view <CAM1|CAM2|PIP|PIP8>  Default view mode on startup (default: CAM1)\n"
                << "  --usage                  Print example launch command and exit\n"
                << "  --gst-verify             Print GStreamer receive pipeline for stream verification and exit\n"
                << "  --version, -v            Print version string and exit\n";
            exit(0);
        } else {
            std::cerr << "Unknown arg: " << arg << " (use --help)" << std::endl;
        }
    }
    return args;
}

// ============================================================================
// Main
// ============================================================================

int main(int argc, char* argv[]) {
    Args args = parseArgs(argc, argv);

    std::cerr << "=== Multi-Camera Streamer ===" << std::endl;
    std::cerr << "Build: " << BUILD_DATE_STR << std::endl;

    g_state.version_word = TRC_VERSION;
    if (args.debugMode) {
        g_state.debugMode.store(true);
        std::cerr << "[Args] Debug logging enabled at launch" << std::endl;
    }
    std::cerr << "Version: "
        << VERSION_MAJOR(g_state.version_word) << "."
        << VERSION_MINOR(g_state.version_word) << "."
        << VERSION_PATCH(g_state.version_word)
        << std::endl;

    // SOM serial — read once from /proc/device-tree/serial-number, parse as decimal
    // uint64. Stored in g_state.somSerial; packed into TelemetryPacket [49-56] every
    // tick by buildTelemetry(). Failure logs a warning and leaves somSerial = 0.
    {
        std::ifstream snFile("/proc/device-tree/serial-number");
        if (snFile.is_open()) {
            std::string snStr;
            std::getline(snFile, snStr);
            // Strip trailing nulls/whitespace (device-tree files often null-terminate)
            while (!snStr.empty() && (snStr.back() == '\0' || snStr.back() == '\n' ||
                snStr.back() == '\r' || snStr.back() == ' '))
                snStr.pop_back();
            try {
                g_state.somSerial = std::stoull(snStr);
                std::cerr << "SOM Serial: " << g_state.somSerial
                    << " (raw: \"" << snStr << "\")" << std::endl;
            }
            catch (const std::exception& e) {
                std::cerr << "SOM Serial: parse failed for \"" << snStr
                    << "\" (" << e.what() << ") — packing 0" << std::endl;
                g_state.somSerial = 0;
            }
        }
        else {
            std::cerr << "SOM Serial: /proc/device-tree/serial-number not readable — packing 0" << std::endl;
            g_state.somSerial = 0;
        }
    }

    // 1. Install signal handlers
    installSignalHandlers();

    // 2. Init GStreamer
    gst_init(&argc, &argv);

    // 3. Create cameras (MWIR defaults to TEST source unless --mwir-live is passed)
    AlviumCamera alvium;
    MwirCamera mwir(args.mwirDevice, /*initialTestMode=*/!args.mwirLive);

    // 4. Init cameras
    if (!alvium.init()) {
        std::cerr << "FATAL: Alvium camera init failed" << std::endl;
        return 1;
    }
    if (!mwir.init()) {
        std::cerr << "FATAL: MWIR camera init failed" << std::endl;
        return 1;
    }

    // 5. Build H.264 encode pipeline
    EncodePipeline encode;
    if (!encode.build(args.destHost, args.videoPort, args.bitrateMbps)) {
        std::cerr << "FATAL: Encode pipeline build failed" << std::endl;
        return 1;
    }

    // 6. Create compositor
    Compositor compositor(&alvium, &mwir, g_state, encode.appsrc);

    // 7. Create UDP listener (A2 — port 10018)
    UdpListener udp(&alvium, &mwir, g_state, args.asciiPort, args.binaryPort);
    udp.jetsonTemp = &compositor.jetsonTemp;
    udp.jetsonCpuLoad = &compositor.jetsonCpuLoad;
    udp.jetsonGpuLoad = &compositor.jetsonGpuLoad;
    udp.setBitrateCallback = [&encode](int mbps) { return encode.setBitrate(mbps); };
    udp.getBitrateCallback = [&encode]() { return encode.getBitrate(); };

    // 7a. Create A1 unsolicited telemetry handler (port 10019 → BDC)
    //     TrcA1 holds references to udp and g_state — both outlive it.
    TrcA1 trcA1(udp, g_state);

    // 8. Set default active camera and view mode
    g_state.activeCamera.store(CameraId::CAM1_ALVIUM);
    // Map --view arg to ViewMode (unrecognised value warns and falls back to CAM1)
    {
        ViewMode defaultView = ViewMode::CAM1;
        if      (args.viewMode == "CAM2") defaultView = ViewMode::CAM2;
        else if (args.viewMode == "PIP")  defaultView = ViewMode::PIP4;
        else if (args.viewMode == "PIP8") defaultView = ViewMode::PIP8;
        else if (args.viewMode != "CAM1")
            std::cerr << "[Args] Unknown --view value '" << args.viewMode
                      << "' — defaulting to CAM1" << std::endl;
        g_state.viewMode.store(defaultView);
    }
    alvium.setActive(true);
    mwir.setActive(false);
    // Apply launch-arg defaults for OSD and focus score to both cameras.
    // Unconditional — main() is authoritative over concrete camera constructors.
    alvium.setOSDEnabled(args.osdEnabled);
    mwir.setOSDEnabled(args.osdEnabled);
    alvium.setFocusScoreEnabled(args.focusScoreEnabled);
    mwir.setFocusScoreEnabled(args.focusScoreEnabled);

    // 9. Start cameras
    if (!alvium.start()) {
        std::cerr << "FATAL: Alvium camera start failed" << std::endl;
        return 1;
    }
    if (!mwir.start()) {
        std::cerr << "FATAL: MWIR camera start failed" << std::endl;
        return 1;
    }

    // 10. Start encode pipeline
    if (!encode.start()) {
        std::cerr << "FATAL: Encode pipeline start failed" << std::endl;
        return 1;
    }

    // 11. Start compositor
    compositor.start();

    // COCO ambient — initialised after compositor is running so camera frames
    // are already flowing before the first inference push fires.
    // 500ms settle: gives Alvium time to deliver live frames post-start.
    if (args.cocoAmbient) {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));
        std::cerr << "[Args] --coco-ambient: loading COCO model..." << std::endl;
        if (alvium.cocoLoadModel()) {
            alvium.setCocoAmbientEnabled(true);
            std::cerr << "[Args] COCO ambient enabled on ALVIUM" << std::endl;
        } else {
            std::cerr << "[Args] WARNING: COCO model load failed — ambient disabled" << std::endl;
        }
    }

    // 12. Start A1 (must start before A2 so BDC telemetry begins immediately)
    trcA1.start();

    // 13. Start UDP listeners (A2 + ASCII)
    udp.start();

    // 14. Start stats thread
    std::thread statsThread(statsThreadFunc, &compositor, &alvium, &mwir, std::ref(g_state));

    std::cerr << "=== Streaming ===" << std::endl;
    std::cerr << "  Dest:    " << args.destHost << ":" << args.videoPort << std::endl;
    std::cerr << "  ASCII:   UDP port " << args.asciiPort << std::endl;
    std::cerr << "  A1:      UDP port " << Defaults::A1_PORT << " → BDC " << Defaults::BDC_HOST << " (100 Hz)" << std::endl;
    std::cerr << "  A2:      UDP port " << args.binaryPort << " (engineering)" << std::endl;

    // 14. Wait for shutdown signal
    while (g_state.running.load()) {
        fd_set fds;
        FD_ZERO(&fds);
        FD_SET(g_signalPipe[0], &fds);
        struct timeval tv { 1, 0 };
        if (select(g_signalPipe[0] + 1, &fds, nullptr, nullptr, &tv) > 0) {
            char tmp;
            if (read(g_signalPipe[0], &tmp, 1) < 0) {}  // drain signal byte
        }
    }

    std::cerr << "\n=== Shutting down ===" << std::endl;

    // 15. Teardown in order (compositor first, then cameras, then comms, then pipeline)
    compositor.stop();
    alvium.stop();
    mwir.stop();
    trcA1.stop();   // A1 before A2 — stops BDC telemetry first
    udp.stop();

    if (statsThread.joinable()) statsThread.join();

    encode.stop();

    if (g_signalPipe[0] >= 0) close(g_signalPipe[0]);
    if (g_signalPipe[1] >= 0) close(g_signalPipe[1]);

    gst_deinit();

    std::cerr << "=== Clean shutdown ===" << std::endl;
    return 0;
}
