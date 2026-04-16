using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.XInput;

namespace CROSSBOW
{
    public class xInput
    {
        private Controller controller;
        private Gamepad gamepad;
        public GamepadButtonFlags buttons;

        public bool isConnected { get { return controller.IsConnected; } }
        // XI-06: deadband field removed — AnalogThumb uses Gamepad.LeftThumbDeadZone /
        //        RightThumbDeadZone directly and this field was never referenced.
        public Point leftThumb = new Point(0, 0);
        private Point lastleftThumb = new Point(0, 0);
        public Point rightThumb = new Point(0, 0);
        private Point lastrightThumb = new Point(0, 0);
        public int leftTrigger, rightTrigger;
        public bool leftTriggerState, rightTriggerState;

        public bool InvertY { get; set; } = false;
        public byte TriggerThreshold { get; set; } = Gamepad.TriggerThreshold; // XI-12: was a raw public field
        public XBOX_SENS_STATES SENSITIVIY { get; set; } = XBOX_SENS_STATES.LINEAR;
        public enum XBOX_SENS_STATES
        {
            CUBERT = -3,
            SQRT = -2,
            LINEAR = 1,
            SQUARED = 2,
            CUBED = 3,
            QUAD = 4
        };

        private CancellationTokenSource? ts;
        private CancellationToken ct;
        private readonly object aLock = new object();

        // XI-01: lastTick removed — timing is now handled by Stopwatch.GetTimestamp()
        //        inside the async poll loop, not by a spin-wait field.


        private Vibration V;
        public bool leftVibrateON
        {
            set
            {
                if (value)
                {
                    V.LeftMotorSpeed = System.Convert.ToUInt16(System.Convert.ToDouble(ushort.MaxValue) * 0.25);
                    V.RightMotorSpeed = 0;
                    controller.SetVibration(V);
                }
                else
                {
                    V.LeftMotorSpeed = 0;
                    V.RightMotorSpeed = 0;
                    controller.SetVibration(V);
                }

            }
        }
        public bool rightVibrateON
        {
            set
            {
                if (value)
                {
                    V.LeftMotorSpeed = 0;
                    V.RightMotorSpeed = System.Convert.ToUInt16(System.Convert.ToDouble(ushort.MaxValue) * 0.25); ;
                    controller.SetVibration(V);
                }
                else
                {
                    V.LeftMotorSpeed = 0;
                    V.RightMotorSpeed = 0;
                    controller.SetVibration(V);
                }

            }
        }


        #region DHAT Up Events
        private ButtonWatch DPadUpButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void DPadUpButtonPressed();
        //declare event of type delegate
        public event DPadUpButtonPressed? onDPadUpButtonPressedEvent;

        public delegate void DPadUpButtonShortPressed();
        //declare event of type delegate
        public event DPadUpButtonShortPressed? onDPadUpButtonShortPressedEvent;

        public delegate void DPadUpButtonLongPressed();
        //declare event of type delegate
        public event DPadUpButtonLongPressed? onDPadUpButtonLongPressedEvent;

        public delegate void DPadUpButtonReleased();
        //declare event of type delegate
        public event DPadUpButtonReleased? onDPadUpButtonReleasedEvent;
        #endregion
        #region DHAT Down Events
        private ButtonWatch DPadDownButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void DPadDownButtonPressed();
        //declare event of type delegate
        public event DPadDownButtonPressed? onDPadDownButtonPressedEvent;

        public delegate void DPadDownButtonShortPressed();
        //declare event of type delegate
        public event DPadDownButtonShortPressed? onDPadDownButtonShortPressedEvent;

        public delegate void DPadDownButtonLongPressed();
        //declare event of type delegate
        public event DPadDownButtonLongPressed? onDPadDownButtonLongPressedEvent;

        public delegate void DPadDownButtonReleased();
        //declare event of type delegate
        public event DPadDownButtonReleased? onDPadDownButtonReleasedEvent;
        #endregion
        #region DHAT Left Events
        private ButtonWatch DPadLeftButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void DPadLeftButtonPressed();
        //declare event of type delegate
        public event DPadLeftButtonPressed? onDPadLeftButtonPressedEvent;

        public delegate void DPadLeftButtonShortPressed();
        //declare event of type delegate
        public event DPadLeftButtonShortPressed? onDPadLeftButtonShortPressedEvent;

        public delegate void DPadLeftButtonLongPressed();
        //declare event of type delegate
        public event DPadLeftButtonLongPressed? onDPadLeftButtonLongPressedEvent;

        public delegate void DPadLeftButtonReleased();
        //declare event of type delegate
        public event DPadLeftButtonReleased? onDPadLeftButtonReleasedEvent;
        #endregion
        #region DHAT Right Events
        private ButtonWatch DPadRightButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void DPadRightButtonPressed();
        //declare event of type delegate
        public event DPadRightButtonPressed? onDPadRightButtonPressedEvent;

        public delegate void DPadRightButtonShortPressed();
        //declare event of type delegate
        public event DPadRightButtonShortPressed? onDPadRightButtonShortPressedEvent;

        public delegate void DPadRightButtonLongPressed();
        //declare event of type delegate
        public event DPadRightButtonLongPressed? onDPadRightButtonLongPressedEvent;

        public delegate void DPadRightButtonReleased();
        //declare event of type delegate
        public event DPadRightButtonReleased? onDPadRightButtonReleasedEvent;
        #endregion

        #region Button A Events
        public ButtonWatch AButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonAPressed();
        //declare event of type delegate
        public event ButtonAPressed? onButtonAPressedEvent;

        public delegate void ButtonAShortPressed();
        //declare event of type delegate
        public event ButtonAShortPressed? onButtonAShortPressedEvent;

        public delegate void ButtonALongPressed();
        //declare event of type delegate
        public event ButtonALongPressed? onButtonALongPressedEvent;

        public delegate void ButtonAReleased();
        //declare event of type delegate
        public event ButtonAReleased? onButtonAReleasedEvent;
        #endregion
        #region Button B Events
        private ButtonWatch BButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonBPressed();
        //declare event of type delegate
        public event ButtonBPressed? onButtonBPressedEvent;

        public delegate void ButtonBShortPressed();
        //declare event of type delegate
        public event ButtonBShortPressed? onButtonBShortPressedEvent;

        public delegate void ButtonBLongPressed();
        //declare event of type delegate
        public event ButtonBLongPressed? onButtonBLongPressedEvent;

        public delegate void ButtonBReleased();
        //declare event of type delegate
        public event ButtonBReleased? onButtonBReleasedEvent;
        #endregion
        #region Button X Events
        private ButtonWatch XButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonXPressed();
        //declare event of type delegate
        public event ButtonXPressed? onButtonXPressedEvent;

        public delegate void ButtonXShortPressed();
        //declare event of type delegate
        public event ButtonXShortPressed? onButtonXShortPressedEvent;

        public delegate void ButtonXLongPressed();
        //declare event of type delegate
        public event ButtonXLongPressed? onButtonXLongPressedEvent;

        public delegate void ButtonXReleased();
        //declare event of type delegate
        public event ButtonXReleased? onButtonXReleasedEvent;
        #endregion
        #region Button Y Events
        private ButtonWatch YButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonYPressed();
        //declare event of type delegate
        public event ButtonYPressed? onButtonYPressedEvent;

        public delegate void ButtonYShortPressed();
        //declare event of type delegate
        public event ButtonYShortPressed? onButtonYShortPressedEvent;

        public delegate void ButtonYLongPressed();
        //declare event of type delegate
        public event ButtonYLongPressed? onButtonYLongPressedEvent;

        public delegate void ButtonYReleased();
        //declare event of type delegate
        public event ButtonYReleased? onButtonYReleasedEvent;
        #endregion

        #region Button Start Events
        private ButtonWatch StartButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonStartPressed();
        //declare event of type delegate
        public event ButtonStartPressed? onButtonStartPressedEvent;

        public delegate void ButtonStartShortPressed();
        //declare event of type delegate
        public event ButtonStartShortPressed? onButtonStartShortPressedEvent;

        public delegate void ButtonStartLongPressed();
        //declare event of type delegate
        public event ButtonStartLongPressed? onButtonStartLongPressedEvent;

        public delegate void ButtonStartReleased();
        //declare event of type delegate
        public event ButtonStartReleased? onButtonStartReleasedEvent;
        #endregion
        #region Button Back Events
        private ButtonWatch BackButtonState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void ButtonBackPressed();
        //declare event of type delegate
        public event ButtonBackPressed? onButtonBackPressedEvent;

        public delegate void ButtonBackShortPressed();
        //declare event of type delegate
        public event ButtonBackShortPressed? onButtonBackShortPressedEvent;

        public delegate void ButtonBackLongPressed();
        //declare event of type delegate
        public event ButtonBackLongPressed? onButtonBackLongPressedEvent;

        public delegate void ButtonBackReleased();
        //declare event of type delegate
        public event ButtonBackReleased? onButtonBackReleasedEvent;
        #endregion

        #region LeftShoulder Events
        public ButtonWatch LeftShoulderState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void LeftShoulderPressed();
        //declare event of type delegate
        public event LeftShoulderPressed? onLeftShoulderPressedEvent;

        public delegate void LeftShoulderShortPressed();
        //declare event of type delegate
        public event LeftShoulderShortPressed? onLeftShoulderShortPressedEvent;

        public delegate void LeftShoulderLongPressed();
        //declare event of type delegate
        public event LeftShoulderLongPressed? onLeftShoulderLongPressedEvent;

        public delegate void LeftShoulderReleased();
        //declare event of type delegate
        public event LeftShoulderReleased? onLeftShoulderReleasedEvent;
        #endregion
        #region RightShoulder Events
        private ButtonWatch RightShoulderState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void RightShoulderPressed();
        //declare event of type delegate
        public event RightShoulderPressed? onRightShoulderPressedEvent;

        public delegate void RightShoulderShortPressed();
        //declare event of type delegate
        public event RightShoulderShortPressed? onRightShoulderShortPressedEvent;

        public delegate void RightShoulderLongPressed();
        //declare event of type delegate
        public event RightShoulderLongPressed? onRightShoulderLongPressedEvent;

        public delegate void RightShoulderReleased();
        //declare event of type delegate
        public event RightShoulderReleased? onRightShoulderReleasedEvent;
        #endregion

        #region Left Trigger State Events
        public ButtonWatch LeftTriggerState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void LeftTriggerPressed();
        //declare event of type delegate
        public event LeftTriggerPressed? onLeftTriggerPressedEvent;

        public delegate void LeftTriggerShortPressed();
        //declare event of type delegate
        public event LeftTriggerShortPressed? onLeftTriggerShortPressedEvent;

        public delegate void LeftTriggerLongPressed();
        //declare event of type delegate
        public event LeftTriggerLongPressed? onLeftTriggerLongPressedEvent;

        public delegate void LeftTriggerReleased();
        //declare event of type delegate
        public event LeftTriggerReleased? onLeftTriggerReleasedEvent;
        #endregion
        #region Right Trigger State Events
        public ButtonWatch RightTriggerState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void RightTriggerPressed();
        //declare event of type delegate
        public event RightTriggerPressed? onRightTriggerPressedEvent;

        public delegate void RightTriggerShortPressed();
        //declare event of type delegate
        public event RightTriggerShortPressed? onRightTriggerShortPressedEvent;

        public delegate void RightTriggerLongPressed();
        //declare event of type delegate
        public event RightTriggerLongPressed? onRightTriggerLongPressedEvent;

        public delegate void RightTriggerReleased();
        //declare event of type delegate
        public event RightTriggerReleased? onRightTriggerReleasedEvent;
        #endregion

        #region Left Hat Events
        private ButtonWatch LeftHatState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void LeftHatPressed();
        //declare event of type delegate
        public event LeftHatPressed? onLeftHatPressedEvent;

        public delegate void LeftHatShortPressed();
        //declare event of type delegate
        public event LeftHatShortPressed? onLeftHatShortPressedEvent;

        public delegate void LeftHatLongPressed();
        //declare event of type delegate
        public event LeftHatLongPressed? onLeftHatLongPressedEvent;

        public delegate void LeftHatReleased();
        //declare event of type delegate
        public event LeftHatReleased? onLeftHatReleasedEvent;
        #endregion
        #region Right Hat Events
        private ButtonWatch RightHatState { get; } = new ButtonWatch();
        // declare delegate 
        public delegate void RightHatPressed();
        //declare event of type delegate
        public event RightHatPressed? onRightHatPressedEvent;

        public delegate void RightHatShortPressed();
        //declare event of type delegate
        public event RightHatShortPressed? onRightHatShortPressedEvent;

        public delegate void RightHatLongPressed();
        //declare event of type delegate
        public event RightHatLongPressed? onRightHatLongPressedEvent;

        public delegate void RightHatReleased();
        //declare event of type delegate
        public event RightHatReleased? onRightHatReleasedEvent;
        #endregion

        #region Left Thumb Events
        public delegate void LeftThumbValueChanged();
        //declare event of type delegate
        public event LeftThumbValueChanged? onLeftThumbValueChangedEvent;
        #endregion
        #region Right Thumb Events
        public delegate void RightThumbValueChanged();
        //declare event of type delegate
        public event RightThumbValueChanged? onRightThumbValueChangedEvent;
        #endregion

        public xInput()
        {
            controller = new Controller(UserIndex.One);
            // XI-04: capture the UI SynchronizationContext at construction time so that
            // ManageEvents() can marshal event invocations back to the UI thread.
            // xInput must be constructed on the UI thread for this to be valid.
            _uiContext = SynchronizationContext.Current;
        }
        private Task? _pollTask; // XI-02: stored so it can be observed on Stop()
        private readonly SynchronizationContext? _uiContext; // XI-04: UI thread context for event marshaling
        // Pre-allocated and reused each poll to avoid per-cycle heap allocation in ManageEvents().
        // Safe to reuse — ManageEvents() runs exclusively on the background poll thread.
        private readonly List<Action> _pendingActions = new List<Action>();

        public void Start()
        {
            ts = new CancellationTokenSource();
            ct = ts.Token;

            // XI-01: Task.Run with async lambda replaces the busy-spin.
            // The loop sleeps for the remainder of each period using Task.Delay,
            // releasing the thread entirely between polls rather than burning CPU.
            // Stopwatch.GetTimestamp() is used for elapsed measurement — it is
            // higher resolution than DateTime.UtcNow.Ticks and avoids a system call.
            _pollTask = Task.Run(async () =>
            {
                Console.WriteLine("XBOX Input Polling Started @ " + Period.ToString() + "ms");
                while (!ct.IsCancellationRequested)
                {
                    long start = Stopwatch.GetTimestamp();

                    Poll();

                    // Calculate how long Poll() took and sleep only the remainder
                    // of the period. If Poll() overruns the period, delay is 0 —
                    // we never sleep longer than Period regardless.
                    long elapsedMs = (Stopwatch.GetTimestamp() - start)
                                     * 1000 / Stopwatch.Frequency;
                    int remaining = Period - (int)elapsedMs;

                    try
                    {
                        if (remaining > 0)
                            await Task.Delay(remaining, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                Console.WriteLine("XBOX Input Polling Stopped");
            }, ct);
        }

        public void Stop()
        {
            ts?.Cancel();
            // _pollTask can be awaited here if a clean join is needed before teardown
        }

        public int Period { get; set; } = 50;

        private void Poll()
        {
            if (!controller.IsConnected)
                return;

            // XI-03: acquire aLock while writing all shared state.
            // Status getter also acquires aLock — without this lock Poll() was writing
            // gamepad, buttons, thumb axes, triggers, and ButtonWatch state on the
            // background thread while the UI thread could read them unprotected.
            // ManageEvents() is called AFTER the lock is released so that event handlers
            // (which may try to read Status or other properties) cannot deadlock.
            lock (aLock)
            {
                gamepad = controller.GetState().Gamepad;
                buttons = gamepad.Buttons;

                leftThumb.X = AnalogThumb(gamepad.LeftThumbX, Gamepad.LeftThumbDeadZone);
                leftThumb.Y = AnalogThumb(gamepad.LeftThumbY, Gamepad.LeftThumbDeadZone);
                rightThumb.X = AnalogThumb(gamepad.RightThumbX, Gamepad.RightThumbDeadZone);
                rightThumb.Y = AnalogThumb(gamepad.RightThumbY, Gamepad.RightThumbDeadZone);
                if (InvertY)
                    rightThumb.Y *= -1;

                leftTrigger = gamepad.LeftTrigger;
                rightTrigger = gamepad.RightTrigger;

                leftTriggerState = gamepad.LeftTrigger > Gamepad.TriggerThreshold;
                rightTriggerState = gamepad.RightTrigger > Gamepad.TriggerThreshold;

                DPadUpButtonState.Update(buttons.HasFlag(GamepadButtonFlags.DPadUp));
                DPadDownButtonState.Update(buttons.HasFlag(GamepadButtonFlags.DPadDown));
                DPadLeftButtonState.Update(buttons.HasFlag(GamepadButtonFlags.DPadLeft));
                DPadRightButtonState.Update(buttons.HasFlag(GamepadButtonFlags.DPadRight));

                AButtonState.Update(buttons.HasFlag(GamepadButtonFlags.A));
                BButtonState.Update(buttons.HasFlag(GamepadButtonFlags.B));
                XButtonState.Update(buttons.HasFlag(GamepadButtonFlags.X));
                YButtonState.Update(buttons.HasFlag(GamepadButtonFlags.Y));

                StartButtonState.Update(buttons.HasFlag(GamepadButtonFlags.Start));
                BackButtonState.Update(buttons.HasFlag(GamepadButtonFlags.Back));

                LeftShoulderState.Update(buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
                RightShoulderState.Update(buttons.HasFlag(GamepadButtonFlags.RightShoulder));

                LeftTriggerState.Update(leftTriggerState);
                RightTriggerState.Update(rightTriggerState);

                LeftHatState.Update(buttons.HasFlag(GamepadButtonFlags.LeftThumb));
                RightHatState.Update(buttons.HasFlag(GamepadButtonFlags.RightThumb));
            }

            // ManageEvents reads ButtonWatch state (written above, single-threaded) and
            // posts event invocations to the UI thread — must run outside the lock.
            ManageEvents();
        }

        public Gamepad Status
        {
            get
            {
                lock (aLock)
                {
                    return gamepad;
                }
            }
        }

        private void ManageEvents()
        {
            // XI-04: event handlers are subscribed by WinForms components on the UI thread.
            // Invoking them directly from the background poll thread causes cross-thread
            // InvalidOperationException when handlers touch any control.
            //
            // Strategy: snapshot each handler delegate now (on the background thread, after
            // Poll() has written all state), decide which events fire, collect the invocations
            // as Actions, then dispatch them all in one SynchronizationContext.Post so every
            // handler runs on the UI thread.
            //
            // Reset() calls stay here on the background thread so the next poll cycle sees
            // clean flags immediately without waiting for the UI thread to process the Post.
            // Reuse the pre-allocated list — clear flags any leftover items from a previous
            // cycle, without triggering a heap allocation. See _pendingActions field comment.
            _pendingActions.Clear();
            var actions = _pendingActions;

            #region DHAT UP
            var _onDPadUpPressed       = onDPadUpButtonPressedEvent;
            var _onDPadUpShortPressed  = onDPadUpButtonShortPressedEvent;
            var _onDPadUpLongPressed   = onDPadUpButtonLongPressedEvent;
            var _onDPadUpReleased      = onDPadUpButtonReleasedEvent;
            if (DPadUpButtonState.onPress    && _onDPadUpPressed      != null) actions.Add(() => _onDPadUpPressed());
            if (DPadUpButtonState.ShortPress && _onDPadUpShortPressed != null) actions.Add(() => _onDPadUpShortPressed());
            if (DPadUpButtonState.LongPress  && _onDPadUpLongPressed  != null) actions.Add(() => _onDPadUpLongPressed());
            if (DPadUpButtonState.Released   && _onDPadUpReleased     != null) actions.Add(() => _onDPadUpReleased());
            if (DPadUpButtonState.ShortPress || DPadUpButtonState.LongPress || DPadUpButtonState.Released) DPadUpButtonState.Reset();
            #endregion

            #region DHAT Down
            var _onDPadDownPressed      = onDPadDownButtonPressedEvent;
            var _onDPadDownShortPressed = onDPadDownButtonShortPressedEvent;
            var _onDPadDownLongPressed  = onDPadDownButtonLongPressedEvent;
            var _onDPadDownReleased     = onDPadDownButtonReleasedEvent;
            if (DPadDownButtonState.onPress    && _onDPadDownPressed      != null) actions.Add(() => _onDPadDownPressed());
            if (DPadDownButtonState.ShortPress && _onDPadDownShortPressed != null) actions.Add(() => _onDPadDownShortPressed());
            if (DPadDownButtonState.LongPress  && _onDPadDownLongPressed  != null) actions.Add(() => _onDPadDownLongPressed());
            if (DPadDownButtonState.Released   && _onDPadDownReleased     != null) actions.Add(() => _onDPadDownReleased());
            if (DPadDownButtonState.ShortPress || DPadDownButtonState.LongPress || DPadDownButtonState.Released) DPadDownButtonState.Reset();
            #endregion

            #region DHAT Left
            var _onDPadLeftPressed      = onDPadLeftButtonPressedEvent;
            var _onDPadLeftShortPressed = onDPadLeftButtonShortPressedEvent;
            var _onDPadLeftLongPressed  = onDPadLeftButtonLongPressedEvent;
            var _onDPadLeftReleased     = onDPadLeftButtonReleasedEvent;
            if (DPadLeftButtonState.onPress    && _onDPadLeftPressed      != null) actions.Add(() => _onDPadLeftPressed());
            if (DPadLeftButtonState.ShortPress && _onDPadLeftShortPressed != null) actions.Add(() => _onDPadLeftShortPressed());
            if (DPadLeftButtonState.LongPress  && _onDPadLeftLongPressed  != null) actions.Add(() => _onDPadLeftLongPressed());
            if (DPadLeftButtonState.Released   && _onDPadLeftReleased     != null) actions.Add(() => _onDPadLeftReleased());
            if (DPadLeftButtonState.ShortPress || DPadLeftButtonState.LongPress || DPadLeftButtonState.Released) DPadLeftButtonState.Reset();
            #endregion

            #region DHAT Right
            var _onDPadRightPressed      = onDPadRightButtonPressedEvent;
            var _onDPadRightShortPressed = onDPadRightButtonShortPressedEvent;
            var _onDPadRightLongPressed  = onDPadRightButtonLongPressedEvent;
            var _onDPadRightReleased     = onDPadRightButtonReleasedEvent;
            if (DPadRightButtonState.onPress    && _onDPadRightPressed      != null) actions.Add(() => _onDPadRightPressed());
            if (DPadRightButtonState.ShortPress && _onDPadRightShortPressed != null) actions.Add(() => _onDPadRightShortPressed());
            if (DPadRightButtonState.LongPress  && _onDPadRightLongPressed  != null) actions.Add(() => _onDPadRightLongPressed());
            if (DPadRightButtonState.Released   && _onDPadRightReleased     != null) actions.Add(() => _onDPadRightReleased());
            if (DPadRightButtonState.ShortPress || DPadRightButtonState.LongPress || DPadRightButtonState.Released) DPadRightButtonState.Reset();
            #endregion

            #region Button A
            var _onAPressed      = onButtonAPressedEvent;
            var _onAShortPressed = onButtonAShortPressedEvent;
            var _onALongPressed  = onButtonALongPressedEvent;
            var _onAReleased     = onButtonAReleasedEvent;
            if (AButtonState.onPress    && _onAPressed      != null) actions.Add(() => _onAPressed());
            if (AButtonState.ShortPress && _onAShortPressed != null) actions.Add(() => _onAShortPressed());
            if (AButtonState.LongPress  && _onALongPressed  != null) actions.Add(() => _onALongPressed());
            if (AButtonState.Released   && _onAReleased     != null) actions.Add(() => _onAReleased());
            if (AButtonState.ShortPress || AButtonState.LongPress || AButtonState.Released) AButtonState.Reset();
            #endregion

            #region Button B
            var _onBPressed      = onButtonBPressedEvent;
            var _onBShortPressed = onButtonBShortPressedEvent;
            var _onBLongPressed  = onButtonBLongPressedEvent;
            var _onBReleased     = onButtonBReleasedEvent;
            if (BButtonState.onPress    && _onBPressed      != null) actions.Add(() => _onBPressed());
            if (BButtonState.ShortPress && _onBShortPressed != null) actions.Add(() => _onBShortPressed());
            if (BButtonState.LongPress  && _onBLongPressed  != null) actions.Add(() => _onBLongPressed());
            if (BButtonState.Released   && _onBReleased     != null) actions.Add(() => _onBReleased());
            if (BButtonState.ShortPress || BButtonState.LongPress || BButtonState.Released) BButtonState.Reset();
            #endregion

            #region Button X
            var _onXPressed      = onButtonXPressedEvent;
            var _onXShortPressed = onButtonXShortPressedEvent;
            var _onXLongPressed  = onButtonXLongPressedEvent;
            var _onXReleased     = onButtonXReleasedEvent;
            if (XButtonState.onPress    && _onXPressed      != null) actions.Add(() => _onXPressed());
            if (XButtonState.ShortPress && _onXShortPressed != null) actions.Add(() => _onXShortPressed());
            if (XButtonState.LongPress  && _onXLongPressed  != null) actions.Add(() => _onXLongPressed());
            if (XButtonState.Released   && _onXReleased     != null) actions.Add(() => _onXReleased());
            if (XButtonState.ShortPress || XButtonState.LongPress || XButtonState.Released) XButtonState.Reset();
            #endregion

            #region Button Y
            var _onYPressed      = onButtonYPressedEvent;
            var _onYShortPressed = onButtonYShortPressedEvent;
            var _onYLongPressed  = onButtonYLongPressedEvent;
            var _onYReleased     = onButtonYReleasedEvent;
            if (YButtonState.onPress    && _onYPressed      != null) actions.Add(() => _onYPressed());
            if (YButtonState.ShortPress && _onYShortPressed != null) actions.Add(() => _onYShortPressed());
            if (YButtonState.LongPress  && _onYLongPressed  != null) actions.Add(() => _onYLongPressed());
            if (YButtonState.Released   && _onYReleased     != null) actions.Add(() => _onYReleased());
            if (YButtonState.ShortPress || YButtonState.LongPress || YButtonState.Released) YButtonState.Reset();
            #endregion

            #region Button Start
            var _onStartPressed      = onButtonStartPressedEvent;
            var _onStartShortPressed = onButtonStartShortPressedEvent;
            var _onStartLongPressed  = onButtonStartLongPressedEvent;
            var _onStartReleased     = onButtonStartReleasedEvent;
            if (StartButtonState.onPress    && _onStartPressed      != null) actions.Add(() => _onStartPressed());
            if (StartButtonState.ShortPress && _onStartShortPressed != null) actions.Add(() => _onStartShortPressed());
            if (StartButtonState.LongPress  && _onStartLongPressed  != null) actions.Add(() => _onStartLongPressed());
            if (StartButtonState.Released   && _onStartReleased     != null) actions.Add(() => _onStartReleased());
            if (StartButtonState.ShortPress || StartButtonState.LongPress || StartButtonState.Released) StartButtonState.Reset();
            #endregion

            #region Button Back
            var _onBackPressed      = onButtonBackPressedEvent;
            var _onBackShortPressed = onButtonBackShortPressedEvent;
            var _onBackLongPressed  = onButtonBackLongPressedEvent;
            var _onBackReleased     = onButtonBackReleasedEvent;
            if (BackButtonState.onPress    && _onBackPressed      != null) actions.Add(() => _onBackPressed());
            if (BackButtonState.ShortPress && _onBackShortPressed != null) actions.Add(() => _onBackShortPressed());
            if (BackButtonState.LongPress  && _onBackLongPressed  != null) actions.Add(() => _onBackLongPressed());
            if (BackButtonState.Released   && _onBackReleased     != null) actions.Add(() => _onBackReleased());
            if (BackButtonState.ShortPress || BackButtonState.LongPress || BackButtonState.Released) BackButtonState.Reset();
            #endregion

            #region LeftShoulder
            var _onLeftShoulderPressed      = onLeftShoulderPressedEvent;
            var _onLeftShoulderShortPressed = onLeftShoulderShortPressedEvent;
            var _onLeftShoulderLongPressed  = onLeftShoulderLongPressedEvent;
            var _onLeftShoulderReleased     = onLeftShoulderReleasedEvent;
            if (LeftShoulderState.onPress    && _onLeftShoulderPressed      != null) actions.Add(() => _onLeftShoulderPressed());
            if (LeftShoulderState.ShortPress && _onLeftShoulderShortPressed != null) actions.Add(() => _onLeftShoulderShortPressed());
            if (LeftShoulderState.LongPress  && _onLeftShoulderLongPressed  != null) actions.Add(() => _onLeftShoulderLongPressed());
            if (LeftShoulderState.Released   && _onLeftShoulderReleased     != null) actions.Add(() => _onLeftShoulderReleased());
            if (LeftShoulderState.ShortPress || LeftShoulderState.LongPress || LeftShoulderState.Released) LeftShoulderState.Reset();
            #endregion

            #region RightShoulder
            var _onRightShoulderPressed      = onRightShoulderPressedEvent;
            var _onRightShoulderShortPressed = onRightShoulderShortPressedEvent;
            var _onRightShoulderLongPressed  = onRightShoulderLongPressedEvent;
            var _onRightShoulderReleased     = onRightShoulderReleasedEvent;
            if (RightShoulderState.onPress    && _onRightShoulderPressed      != null) actions.Add(() => _onRightShoulderPressed());
            if (RightShoulderState.ShortPress && _onRightShoulderShortPressed != null) actions.Add(() => _onRightShoulderShortPressed());
            if (RightShoulderState.LongPress  && _onRightShoulderLongPressed  != null) actions.Add(() => _onRightShoulderLongPressed());
            if (RightShoulderState.Released   && _onRightShoulderReleased     != null) actions.Add(() => _onRightShoulderReleased());
            if (RightShoulderState.ShortPress || RightShoulderState.LongPress || RightShoulderState.Released) RightShoulderState.Reset();
            #endregion

            #region Left Trigger State
            var _onLeftTriggerPressed      = onLeftTriggerPressedEvent;
            var _onLeftTriggerShortPressed = onLeftTriggerShortPressedEvent;
            var _onLeftTriggerLongPressed  = onLeftTriggerLongPressedEvent;
            var _onLeftTriggerReleased     = onLeftTriggerReleasedEvent;
            if (LeftTriggerState.onPress    && _onLeftTriggerPressed      != null) actions.Add(() => _onLeftTriggerPressed());
            if (LeftTriggerState.ShortPress && _onLeftTriggerShortPressed != null) actions.Add(() => _onLeftTriggerShortPressed());
            if (LeftTriggerState.LongPress  && _onLeftTriggerLongPressed  != null) actions.Add(() => _onLeftTriggerLongPressed());
            if (LeftTriggerState.Released   && _onLeftTriggerReleased     != null) actions.Add(() => _onLeftTriggerReleased());
            if (LeftTriggerState.ShortPress || LeftTriggerState.LongPress || LeftTriggerState.Released) LeftTriggerState.Reset();
            #endregion

            #region Right Trigger State
            var _onRightTriggerPressed      = onRightTriggerPressedEvent;
            var _onRightTriggerShortPressed = onRightTriggerShortPressedEvent;
            var _onRightTriggerLongPressed  = onRightTriggerLongPressedEvent;
            var _onRightTriggerReleased     = onRightTriggerReleasedEvent;
            if (RightTriggerState.onPress    && _onRightTriggerPressed      != null) actions.Add(() => _onRightTriggerPressed());
            if (RightTriggerState.ShortPress && _onRightTriggerShortPressed != null) actions.Add(() => _onRightTriggerShortPressed());
            if (RightTriggerState.LongPress  && _onRightTriggerLongPressed  != null) actions.Add(() => _onRightTriggerLongPressed());
            if (RightTriggerState.Released   && _onRightTriggerReleased     != null) actions.Add(() => _onRightTriggerReleased());
            if (RightTriggerState.ShortPress || RightTriggerState.LongPress || RightTriggerState.Released) RightTriggerState.Reset();
            #endregion

            #region Left Hat
            var _onLeftHatPressed      = onLeftHatPressedEvent;
            var _onLeftHatShortPressed = onLeftHatShortPressedEvent;
            var _onLeftHatLongPressed  = onLeftHatLongPressedEvent;
            var _onLeftHatReleased     = onLeftHatReleasedEvent;
            if (LeftHatState.onPress    && _onLeftHatPressed      != null) actions.Add(() => _onLeftHatPressed());
            if (LeftHatState.ShortPress && _onLeftHatShortPressed != null) actions.Add(() => _onLeftHatShortPressed());
            if (LeftHatState.LongPress  && _onLeftHatLongPressed  != null) actions.Add(() => _onLeftHatLongPressed());
            if (LeftHatState.Released   && _onLeftHatReleased     != null) actions.Add(() => _onLeftHatReleased());
            if (LeftHatState.ShortPress || LeftHatState.LongPress || LeftHatState.Released) LeftHatState.Reset();
            #endregion

            #region Right Hat
            var _onRightHatPressed      = onRightHatPressedEvent;
            var _onRightHatShortPressed = onRightHatShortPressedEvent;
            var _onRightHatLongPressed  = onRightHatLongPressedEvent;
            var _onRightHatReleased     = onRightHatReleasedEvent;
            if (RightHatState.onPress    && _onRightHatPressed      != null) actions.Add(() => _onRightHatPressed());
            if (RightHatState.ShortPress && _onRightHatShortPressed != null) actions.Add(() => _onRightHatShortPressed());
            if (RightHatState.LongPress  && _onRightHatLongPressed  != null) actions.Add(() => _onRightHatLongPressed());
            if (RightHatState.Released   && _onRightHatReleased     != null) actions.Add(() => _onRightHatReleased());
            if (RightHatState.ShortPress || RightHatState.LongPress || RightHatState.Released) RightHatState.Reset();
            #endregion

            #region Left Thumb
            var _onLeftThumb = onLeftThumbValueChangedEvent;
            if (_onLeftThumb != null &&
                (leftThumb.X != 0 || leftThumb.Y != 0 ||
                 (leftThumb.X == 0 && lastleftThumb.X != 0) ||
                 (leftThumb.Y == 0 && lastleftThumb.Y != 0)))
            {
                // XI-04: fire directly on background thread — handler reads XB.leftThumb
                // at call time. Posting async would let Poll() overwrite leftThumb before
                // the UI thread runs the callback, reading zero instead of the live value.
                // Handler only updates plain data (TRACK_GATE_CENTER/SIZE), not controls.
                //
                // Note: fires on every poll while stick is non-zero (not just on change).
                // The frmMain handler uses an accumulator pattern (x += leftThumb.X / 10)
                // that requires continuous firing while the stick is held in a direction.
                _onLeftThumb();
                lastleftThumb = leftThumb;
            }
            #endregion

            #region Right Thumb
            var _onRightThumb = onRightThumbValueChangedEvent;
            if (_onRightThumb != null &&
                (rightThumb.X != 0 || rightThumb.Y != 0 ||
                 (rightThumb.X == 0 && lastrightThumb.X != 0) ||
                 (rightThumb.Y == 0 && lastrightThumb.Y != 0)))
            {
                // Same reasoning as Left Thumb above.
                _onRightThumb();
                lastrightThumb = rightThumb;
            }
            #endregion

            // XI-04: dispatch button/trigger event invocations to the UI thread in one Post.
            // Post is async — the background poll thread continues immediately without
            // blocking on UI thread availability. If no context was captured (e.g. unit
            // test environment), fall back to direct invocation on the calling thread.
            //
            // IMPORTANT: snapshot to an array before calling Post. _pendingActions is cleared
            // at the top of the next ManageEvents() call (50ms later). Post is async — the UI
            // thread may not have processed the callback yet when that Clear() runs, which
            // would silently drop all queued events. The array snapshot is owned exclusively
            // by the closure and is not affected by the subsequent Clear().
            if (actions.Count > 0)
            {
                var snapshot = actions.ToArray();
                if (_uiContext != null)
                    _uiContext.Post(_ => { foreach (var a in snapshot) a(); }, null);
                else
                    foreach (var a in snapshot) a();
            }
        }

        private int AnalogThumb(int _ival, short _deadband)
        {

            // verify against deadband and return as %
            double val = System.Convert.ToDouble(_ival);
            if (Math.Abs(val) < _deadband)
            {
                return 0;
            }
            else
            {
                double temp1 = val / System.Convert.ToDouble(short.MaxValue) * 100.0;
                int s = Math.Sign(temp1);
                temp1 = Math.Abs(temp1);
                return System.Convert.ToInt32(s * xbox_scale(temp1));
            }

        }

        private double xbox_scale(double _absv)
        {
            //flag: -3 cube rt, -2 sqrt, 1 =linear, 2=^2, 3=cube
            // XI-11: cast SENSITIVIY to int once rather than twice
            int sens = (int)SENSITIVIY;
            double pow = sens >= 0 ? sens : 1.0 / Math.Abs(sens);
            return Math.Pow(_absv / 100.0, pow) * 100.0;
        }


    }

    public class ButtonWatch
    {
        // XI-09: output flags are read-only externally — only Update() and Reset() should
        // set them. Removing public set prevents external code from corrupting the state machine.
        public bool onPress { get; private set; }
        public bool Pressed { get; private set; }
        public bool Released { get; private set; }
        public bool ShortPress { get; private set; }
        public bool LongPress { get; private set; } = false;
        private bool PreviousState;
        Int64 t0, t1, dt;
        public long ShortPressTime { get; set; }
        public long LongPressTime { get; set; }
        public ButtonWatch(long _shortLimit = 100, long _pressLimit = 2000)
        {
            ShortPressTime = _shortLimit;
            LongPressTime = _pressLimit;
            onPress = false;
            Pressed = false;
            Released = false;
            ShortPress = false;
            LongPress = false;
            PreviousState = false;

        }
        public void Reset()
        {
            onPress = false;
            Pressed = false;
            Released = false;
            ShortPress = false;
            LongPress = false;
            // XI-05: PreviousState intentionally NOT cleared here.
            // Reset() clears the output flags only. If the button is still physically
            // held when Reset() is called, clearing PreviousState would cause the next
            // Update(true) to see Pressed=true + PreviousState=false and fire a
            // spurious onPress event. PreviousState is managed exclusively by Update().
        }
        public void SetTimes(long _s, long _t)
        {
            ShortPressTime = _s;
            LongPressTime = _t;
        }
        public void Update(bool _currentState)
        {
            Pressed = _currentState;

            if (Pressed && !PreviousState)
            {
                onPress = true;
            }
            else
            {
                onPress = false;
            }

            if (_currentState)  // pressed
            {
                // first time through, start counter
                if (!PreviousState)
                {
                    t0 = DateTime.UtcNow.Ticks;
                }

                PreviousState = true;
                Released = false;
                ShortPress = false;
                LongPress = false;
            }
            else
            {
                if (PreviousState) // released
                {
                    PreviousState = false;
                    Released = true;

                    t1 = DateTime.UtcNow.Ticks; // 1 TICK = 100 ns
                    dt = (t1 - t0) / 10_000; // XI-10: integer division — exact, no float precision loss
                    if (dt >= ShortPressTime)
                        ShortPress = true;

                    if (dt >= LongPressTime)
                    {
                        LongPress = true;
                        ShortPress = false; // only want one or the other
                    }
                }
            }

        }

    }

}
