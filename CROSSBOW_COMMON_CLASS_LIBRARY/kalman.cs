using GeographicLib;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    internal class KALMAN
    {
        // State vector X = [N, E, D, vN, vE, vD] in local NED (metres, m/s)
        //
        // Measurement model H — all 6 states now directly observed.
        // Issue 25 resolved: vD is supplied by LLA2NED() from real sensor vertical rate
        // (ADS-B VerticalRate_mps, ECHO VELOCITY_ENU.z, RADAR vz). H[5,5] promoted to 1.0.
        private readonly Matrix<double> H = DenseMatrix.OfArray(new double[,] {
            { 1.0, 0.0, 0.0, 0.0, 0.0, 0.0 },  // observe N
            { 0.0, 1.0, 0.0, 0.0, 0.0, 0.0 },  // observe E
            { 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 },  // observe D
            { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0 },  // observe vN
            { 0.0, 0.0, 0.0, 0.0, 1.0, 0.0 },  // observe vE
            { 0.0, 0.0, 0.0, 0.0, 0.0, 1.0 }}); // observe vD (Issue 25 resolved)

        // _stateLock guards all reads and writes of _XX and _lastUpdateTime.
        //
        // Root cause of discontinuous display jumps (Rev 14 fix):
        //   PredictedPosition() runs on the UI timer thread (50 ms).
        //   Update() runs on the sensor receive thread (1–10 Hz).
        //   Without a lock the timer thread could read new _XX paired with the old
        //   _lastUpdateTime (or vice versa), computing dt ≈ 1.05 s instead of 0.05 s
        //   and projecting the position ~1 second forward — a visible jump every time
        //   the two threads overlapped.  A single lock makes the snapshot atomic.
        //
        //   Lock scope in PredictedPosition: snapshot only — F(dt)*snapshot computed
        //   outside the lock so the sensor thread is never stalled by the UI thread.
        //   Lock scope in Update/init: entire state mutation, since sensor updates are
        //   infrequent (≤10 Hz) and matrix ops complete in microseconds.
        private readonly object _stateLock = new object();

        private Matrix<double>? R, P;
        private Vector<double>? _XX;
        private DateTime _lastUpdateTime = DateTime.UtcNow;

        /// <summary>True once init() has been called and _XX is valid.</summary>
        public bool IsInitialised { get; private set; } = false;

        /// <summary>Timestamp of the most recent measurement passed to init() or Update().</summary>
        public DateTime LastUpdateTime
        {
            get { lock (_stateLock) { return _lastUpdateTime; } }
        }

        // Measurement noise — split by type (Issue 16 / Issue 17):
        //   R_pos: CPR quantisation ~5 m → σ² = 25 m²  (use ~4 m² for ECHO mmWave)
        //   R_vel: heading+speed decomposition uncertainty ~2 m/s → σ² = 4 (m/s)²
        private double R_pos { get; set; } = 25.0;
        private double R_vel { get; set; } = 4.0;

        // Initial state covariance scale — match R_pos at first fix (Issue 17)
        private double P0 { get; set; } = 25.0;

        private Matrix<double> getFdt(double dt)
        {
            return DenseMatrix.OfArray(new double[,] {
                            { 1.0, 0.0, 0.0, dt,  0.0, 0.0 },
                            { 0.0, 1.0, 0.0, 0.0, dt,  0.0 },
                            { 0.0, 0.0, 1.0, 0.0, 0.0, dt  },
                            { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0 },
                            { 0.0, 0.0, 0.0, 0.0, 1.0, 0.0 },
                            { 0.0, 0.0, 0.0, 0.0, 0.0, 1.0 }});
        }
        private Matrix<double> getQdt(double dt)
        {
            // White-noise acceleration model: Q = σ_a² × [dt⁴/4  dt³/2]  per axis
            //                                              [dt³/2  dt²  ]
            // σ_a² = acceleration noise variance (m/s²)².
            // 4.5 ≈ σ_a = 2.1 m/s² — reasonable for commercial aircraft cruise.
            // Increase to 25–100 for agile UAV/LoRa targets.
            const double sigma_a_sq = 4.5;
            double dt2   = sigma_a_sq * dt * dt;
            double dt3_2 = dt * dt2 / 2;
            double dt4_4 = dt * dt * dt2 / 4;

            return DenseMatrix.OfArray(new double[,] {
                            { dt4_4, 0.0,   0.0,   dt3_2, 0.0,   0.0   },
                            { 0.0,   dt4_4, 0.0,   0.0,   dt3_2, 0.0   },
                            { 0.0,   0.0,   dt4_4, 0.0,   0.0,   dt3_2 },
                            { dt3_2, 0.0,   0.0,   dt2,   0.0,   0.0   },
                            { 0.0,   dt3_2, 0.0,   0.0,   dt2,   0.0   },
                            { 0.0,   0.0,   dt3_2, 0.0,   0.0,   dt2   } });
        }
        private Matrix<double> getP()
        {
            // Shape matrix multiplied by P0 scalar in init().
            // Position diagonal × P0=25 → σ_pos = 5 m (matches R_pos).
            // Velocity diagonal × P0=25 → σ_vel = 50 m/s (wide prior; filter converges quickly).
            return DenseMatrix.OfArray(new double[,] {
                            { 1.0, 0.0, 0.0, 0.0,   0.0,   0.0   },
                            { 0.0, 1.0, 0.0, 0.0,   0.0,   0.0   },
                            { 0.0, 0.0, 1.0, 0.0,   0.0,   0.0   },
                            { 0.0, 0.0, 0.0, 100.0, 0.0,   0.0   },
                            { 0.0, 0.0, 0.0, 0.0,   100.0, 0.0   },
                            { 0.0, 0.0, 0.0, 0.0,   0.0,   100.0 }});
        }

        public KALMAN() { }

        /// <summary>
        /// Returns the predicted NED state vector [N, E, D, vN, vE, vD] propagated forward
        /// to the current wall-clock time.  Safe to call from any thread at any time —
        /// _XX and _lastUpdateTime are snapshotted atomically under _stateLock, and the
        /// F(dt) projection is computed outside the lock.
        /// Returns null if init() has not yet been called.
        /// </summary>
        public Vector<double>? PredictedPosition()
        {
            Vector<double> snapshot;
            DateTime snapshotTime;
            lock (_stateLock)
            {
                if (!IsInitialised) return null;
                snapshot     = _XX!;
                snapshotTime = _lastUpdateTime;
            }
            // Compute F(dt) × snapshot outside the lock — read-only, no shared state mutated.
            double dt = Math.Max((DateTime.UtcNow - snapshotTime).TotalSeconds, 0.0);
            return getFdt(dt) * snapshot;
        }

        public ptLLA PredictedPosition(ptLLA _baseStation)
        {
            Vector<double>? Z = PredictedPosition();
            if (Z == null) return new ptLLA(0, 0, 0);
            return COMMON.ned2lla(new double3(Z[0], Z[1], Z[2]), _baseStation);
        }

        public Vector<double>? LatestPosition()
        {
            lock (_stateLock) { return _XX; }
        }
        public ptLLA LatestPosition(ptLLA _baseStation)
        {
            Vector<double>? Z = LatestPosition();
            if (Z == null) return new ptLLA(0, 0, 0);
            return COMMON.ned2lla(new double3(Z[0], Z[1], Z[2]), _baseStation);
        }

        /// <summary>
        /// Initialise the filter from the first measurement.
        /// <paramref name="measurementTime"/> is the timestamp of the measurement, used to
        /// seed _lastUpdateTime so the first Update() dt is computed from message time, not
        /// wall-clock time — eliminating the burst-processing dt error (Rev 14).
        /// </summary>
        public void init(Vector<double> Z, DateTime measurementTime)
        {
            // Build split measurement noise matrix: position rows use R_pos, velocity rows use R_vel.
            R = Matrix<double>.Build.Dense(6, 6, 0.0);
            R[0, 0] = R_pos; R[1, 1] = R_pos; R[2, 2] = R_pos;  // N, E, D  noise (m²)
            R[3, 3] = R_vel; R[4, 4] = R_vel; R[5, 5] = R_vel;  // vN, vE, vD noise ((m/s)²)

            var F0 = getFdt(0);
            P = F0 * (getP() * P0) * F0.Transpose() + getQdt(0);

            lock (_stateLock)
            {
                _XX             = F0 * Z;
                _lastUpdateTime = measurementTime;
                IsInitialised   = true;
            }
        }

        /// <summary>
        /// Incorporate a new measurement Z = [N, E, D, vN, vE, vD].
        /// <paramref name="measurementTime"/> is the timestamp of the measurement packet.
        /// Using message time rather than DateTime.UtcNow prevents burst-processing dt errors
        /// where back-to-back packets would be assigned dt ≈ 0 instead of their true interval.
        /// </summary>
        public void Update(Vector<double> Z, DateTime measurementTime)
        {
            lock (_stateLock)
            {
                if (!IsInitialised) return;

                // dt from measurement timestamps — immune to processing delays and bursts.
                // Clamp to 1 ms minimum to guard against duplicate or out-of-order packets.
                double dt = Math.Max((measurementTime - _lastUpdateTime).TotalSeconds, 1e-3);
                var F = getFdt(dt);
                var Q = getQdt(dt);

                // Predict
                _XX = F * _XX;
                P   = F * P * F.Transpose() + Q;

                // Update
                Matrix<double> S = H * P * H.Transpose() + R;   // 6×6 innovation covariance
                Matrix<double> K = P * H.Transpose() * S.Inverse(); // 6×6 Kalman gain
                _XX = _XX + K * (Z - H * _XX);                  // apply innovation
                P   = (Matrix<double>.Build.DenseIdentity(6) - K * H) * P;

                _lastUpdateTime = measurementTime;
            }
        }
    }

    public class KalmanFilter
    {
        private Vector<double> state; // [bearing, bearing_rate]
        private Matrix<double> covariance;
        private Matrix<double> observationMatrix; // H
        private Matrix<double> processNoise; // Q
        private Matrix<double> measurementNoise; // R

        private DateTime LastUpdateTime = DateTime.UtcNow; // Issue 19: track actual elapsed time

        public KalmanFilter(double initialBearing)
        {
            state = Vector<double>.Build.DenseOfArray(new[] { initialBearing, 0.0 });
            covariance = Matrix<double>.Build.DenseOfDiagonalArray(new[] { 1.0, 1.0 });

            // H: observe bearing only (1×2)
            observationMatrix = Matrix<double>.Build.DenseOfArray(new double[,] { { 1, 0 } });

            // Process noise and measurement noise — tuned for ~1 Hz bearing updates
            processNoise      = Matrix<double>.Build.DenseOfDiagonalArray(new[] { 0.1, 0.1 });
            measurementNoise  = Matrix<double>.Build.DenseOfDiagonalArray(new[] { 0.5 });
        }

        public double Filter(double newBearing)
        {
            // Issue 19: compute actual elapsed time instead of assuming dt=1.0 s.
            // dt=1.0 was correct at 1 Hz but wrong at any other update rate.
            double dt = Math.Max((DateTime.UtcNow - LastUpdateTime).TotalSeconds, 1e-3);
            LastUpdateTime = DateTime.UtcNow;

            // F(dt): constant-bearing-rate transition rebuilt each call
            var transitionMatrix = Matrix<double>.Build.DenseOfArray(new double[,]
            {
                { 1, dt },
                { 0, 1  }
            });

            // 1. Predict
            Vector<double> predictedState      = transitionMatrix * state;
            Matrix<double> predictedCovariance = transitionMatrix * covariance * transitionMatrix.Transpose() + processNoise;

            // 2. Update
            Vector<double> measurement         = Vector<double>.Build.DenseOfArray(new[] { newBearing });
            Vector<double> innovation          = measurement - observationMatrix * predictedState;
            Matrix<double> innovationCovariance = observationMatrix * predictedCovariance * observationMatrix.Transpose() + measurementNoise;
            Matrix<double> kalmanGain          = predictedCovariance * observationMatrix.Transpose() * innovationCovariance.Inverse();

            state      = predictedState + kalmanGain * innovation;
            covariance = (Matrix<double>.Build.DenseIdentity(2) - kalmanGain * observationMatrix) * predictedCovariance;

            return state[0]; // smoothed bearing
        }
    }

}
