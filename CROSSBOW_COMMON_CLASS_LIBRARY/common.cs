using GeographicLib;
using GMap.NET;
using GMap.NET.WindowsForms;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CROSSBOW
{
    public class COMMON
    {

        public static float fmod(float a, float b)
        {
            return a - b * (float)Math.Floor(a / b);
        }
        public static double dmod(double a, double b)
        {
            return a - b * Math.Floor(a / b);
        }

        public static double PI = Math.PI;
        public static double deg2rad(double deg) { return deg * PI / 180.0; }
        public static double rad2deg(double rad) { return rad * 180.0 / PI; }

        public static double ft2m(double ft) { return ft * 0.3048; }
        public static double m2ft(double m) { return m / 0.3048; }
        public static double m2NM(double m) { return m * 0.000539957; }
        public static double kts2mps(double m) { return m * 0.514444; }
        public static ptLLA projectLLA(ptLLA a, double dist_m, double truecoure_deg)
        {
            Geodesic geod = new Geodesic(Ellipsoid.WGS84);
            // Alternatively: Geodesic geod = new Geodesic();
            double lat2, lon2;
            double a12 = geod.Direct(a.lat, a.lng, truecoure_deg, dist_m, out lat2, out lon2);
            //Console.WriteLine("a12 = " + a12.ToString());
            return new ptLLA(lat2, lon2, a.alt);
        }

        public static double GetBearing(double dLat, double dLon)
        {
            double B = Math.Atan2(dLon, dLat) * 180.0 / Math.PI;  // atan2(y,x)->theta; atan2(x,y)->Bearing
            return B;
        }

        public static double GetBearing(ptLLA a, ptLLA b)
        {
            Geodesic geod = new Geodesic(Ellipsoid.WGS84);
            double foo = geod.Inverse(a.lat, a.lng, b.lat, b.lng, out double az1, out double az2);
            double bearing = az1; //Math.Atan2(y, x) * 180.0 / Math.PI;
            return (bearing + 360) % 360; // not sure this is needed
        }

        public static double GetElevation(ptLLA a, ptLLA b)
        {
            // get elevation in ECEF given LLA
            Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
            (double Xa, double Ya, double Za) = earth.Forward(a.lat, a.lng, a.alt);
            (double Xb, double Yb, double Zb) = earth.Forward(b.lat, b.lng, b.alt);

            double3 Va = new double3(Xa, Ya, Za);
            double3 Vb = new double3(Xb, Yb, Zb);

            double3 dV = Vb - Va;

            double el = double3.dot(Va, dV);
            el = el / (double3.norm(Va) * double3.norm(dV));
            el = 90.0 - Math.Acos(el) * 180.0 / Math.PI;
            return el;

        }
        public static double geoDist(ptLLA a, ptLLA b)
        {
            // replaces Haversine
            Geodesic geod = new Geodesic(Ellipsoid.WGS84);
            double s12;
            geod.Inverse(a.lat, a.lng, b.lat, b.lng, out s12);

            return s12;
        }

        public static double haversine(double lat1, double lon1, double alt1, double lat2, double lon2, double alt2)
        {
            double deg2rad = 3.14159265358979323846 / 180.0;
            // Earth's radius in kilometers (mean radius) -> meters
            const double R = 6371.0 * 1000;

            // Convert latitudes and longitudes from degrees to radians
            lat1 *= deg2rad;
            lon1 *= deg2rad;
            lat2 *= deg2rad;
            lon2 *= deg2rad;

            // Calculate differences in latitude and longitude
            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;

            // Apply the Haversine formula components
            double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            // Calculate the distance
            return R * c + 0 * Math.Abs(alt2 - alt1); // add delta alt ??

        }

        public static double3 lla2ned(ptLLA _pos, ptLLA _ref)
        {
            // get elevation in ECEF given LLA
            Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
            (double Xpos, double Ypos, double Zpos) = earth.Forward(_pos.lat, _pos.lng, _pos.alt);
            (double Xref, double Yref, double Zref) = earth.Forward(_ref.lat, _ref.lng, _ref.alt);

            double u = Xpos - Xref;
            double v = Ypos - Yref;
            double w = Zpos - Zref;

            double cosPhi = Math.Cos(deg2rad(_ref.lat));
            double sinPhi = Math.Sin(deg2rad(_ref.lat));
            double cosLambda = Math.Cos(deg2rad(_ref.lng));
            double sinLambda = Math.Sin(deg2rad(_ref.lng));

            double t = cosLambda * u + sinLambda * v;
            double uEast = -sinLambda * u + cosLambda * v;

            double wUp = cosPhi * t + sinPhi * w;
            double vNorth = -sinPhi * t + cosPhi * w;

            double zDown = -wUp;

            return new double3(vNorth, uEast, zDown);
        }

        public static ptLLA ned2lla(double3 _ned, ptLLA _ref)
        {
            // ned -> enu
            double vNorth = _ned.x;
            double uEast = _ned.y;
            double wUp = -_ned.z;

            double cosPhi = Math.Cos(deg2rad(_ref.lat));
            double sinPhi = Math.Sin(deg2rad(_ref.lat));
            double cosLambda = Math.Cos(deg2rad(_ref.lng));
            double sinLambda = Math.Sin(deg2rad(_ref.lng));

            Geocentric earth = new Geocentric(Ellipsoid.WGS84); //new Geocentric(Constants.WGS84.MajorRadius, Constants.WGS84.Flattening);
            (double x0, double y0, double z0) = earth.Forward(_ref.lat, _ref.lng, _ref.alt);

            // Rotate ENU to ECEF frame(origin is the reference LLA coordinates)
            // rotENU2ECEF = Rz(-(pi / 2 + lambda)) * Ry(0) * Rx(-(pi / 2 - phi))
            // rotENU2ECEF = [-sinlambda -coslambda.*sinphi coslambda.*cosphi
            //                 coslambda -sinlambda.*sinphi sinlambda.*cosphi
            //                     0            cosphi            sinphi     ];

            double tmp = cosPhi * wUp - sinPhi * vNorth;
            double dx = cosLambda * tmp - sinLambda * uEast;
            double dy = sinLambda * tmp + cosLambda * uEast;
            double dz = sinPhi * wUp + cosPhi * vNorth;

            // Translate values so that origin aligns with Earth's origin.
            double x = x0 + dx;
            double y = y0 + dy;
            double z = z0 + dz;

            (double lat, double lng, double alt) = earth.Reverse(x, y, z);

            return new ptLLA(lat, lng, alt);
        }

        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + GetHexVal(hex[(i << 1) + 1]));
            }

            return arr;
        }
        public static Bitmap RotateImage2(Bitmap bmp, float angle)
        {
            float height = bmp.Height;
            float width = bmp.Width;
            int hypotenuse = Convert.ToInt32(Math.Floor(Math.Sqrt(height * height + width * width)));
            Bitmap rotatedImage = new Bitmap(hypotenuse, hypotenuse);
            using (Graphics g = Graphics.FromImage(rotatedImage))
            {
                g.TranslateTransform((float)rotatedImage.Width / 2, (float)rotatedImage.Height / 2); //set the rotation point as the center into the matrix
                g.RotateTransform(angle); //rotate
                g.TranslateTransform(-(float)rotatedImage.Width / 2, -(float)rotatedImage.Height / 2); //restore rotation point into the matrix
                g.DrawImage(bmp, (hypotenuse - width) / 2, (hypotenuse - height) / 2, width, height);
            }
            return rotatedImage;
        }
        public static int GetHexVal(char hex)
        {
            int val = hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : val < 97 ? 55 : 87);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class float3
    {
        public float x;
        public float y;
        public float z;

        public float3() { }
        public float3(float a, float b, float c)
        {
            x = a;
            y = b;
            z = c;
        }
        public static float3 operator +(float3 a, float3 b)
        {
            return new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        }
        public static float3 operator -(float3 a, float3 b)
        {
            return new float3(a.x - b.x, a.y - b.y, a.z - b.z);
        }
        public static float3 operator *(float a, float3 b)
        {
            return new float3(a * b.x, a * b.y, a * b.z);
        }
        public static float3 operator *(int a, float3 b)
        {
            return new float3(a * b.x, a * b.y, a * b.z);
        }
        public static float3 operator *(float3 a, float b)
        {
            return b * a;
        }
        public static float3 operator *(float3 a, int b)
        {
            return b * a;
        }
        public static float3 operator /(float3 a, float b)
        {
            return new float3(a.x / b, a.y / b, a.z / b);
        }
        public override string ToString()
        {
            return "<" + x.ToString() + "; " + y.ToString() + "; " + z.ToString() + ">";
        }
        public static double dot(float3 v1, float3 v2)
        {
            // return dot product of 2 vectors
            return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
        }
        public static double norm(float3 v)
        {
            // return norm of vector
            return Math.Sqrt(dot(v, v));
        }
        public static float3 units(float3 v)
        {
            // return unit vector uv
            float lv = (float)norm(v);
            return v / lv;
        }
        //public float3 cross(float3 v1, float3 v2)
        //{
        //    // cross product of 2 vectors
        //    return new double3((v1.y * v2.z) - (v1.z * v2.y), (v1.z * v2.x) - (v1.x * v2.z), (v1.x * v2.y) - (v1.y * v2.x));
        //}
        public static double dist(float3 p1, float3 p2)
        {
            // calculate distance between 2 points
            return norm(p1 - p2);
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public class double3
    {
        public double x;
        public double y;
        public double z;

        public double3() { }
        public double3(double a, double b, double c)
        {
            x = a;
            y = b;
            z = c;
        }
        public static double3 operator +(double3 a, double3 b)
        {
            return new double3(a.x + b.x, a.y + b.y, a.z + b.z);
        }
        public static double3 operator -(double3 a, double3 b)
        {
            return new double3(a.x - b.x, a.y - b.y, a.z - b.z);
        }
        public static double3 operator *(double a, double3 b)
        {
            return new double3(a * b.x, a * b.y, a * b.z);
        }
        public static double3 operator *(int a, double3 b)
        {
            return new double3(a * b.x, a * b.y, a * b.z);
        }
        public static double3 operator *(double3 a, double b)
        {
            return b * a;
        }
        public static double3 operator *(double3 a, int b)
        {
            return b * a;
        }
        public static double3 operator /(double3 a, double b)
        {
            return new double3(a.x / b, a.y / b, a.z / b);



        }
        public override string ToString()
        {
            return "<" + x.ToString() + "; " + y.ToString() + "; " + z.ToString() + ">";
        }
        public static double dot(double3 v1, double3 v2)
        {
            // return dot product of 2 vectors
            return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
        }
        public static double norm(double3 v)
        {
            // return norm of vector
            return Math.Sqrt(dot(v, v));
        }
        public static double3 units(double3 v)
        {
            // return unit vector uv
            double lv = norm(v);
            return v / lv;
        }
        //public double3 cross(double3 v1, double3 v2)
        //{
        //    // cross product of 2 vectors
        //    return new double3((v1.y * v2.z) - (v1.z * v2.y), (v1.z * v2.x) - (v1.x * v2.z), (v1.x * v2.y) - (v1.y * v2.x));
        //}
        public static double dist(double3 p1, double3 p2)
        {
            // calculate distance between 2 points
            return norm(p1 - p2);
        }

    }

    public class ptLLA
    {
        public double lat;
        public double lng;
        public double alt;
        public ptLLA() { }
        public ptLLA(double _lat, double _lng, double _alt)
        {
            lat = _lat;
            lng = _lng;
            alt = _alt;
        }
        public static ptLLA operator +(ptLLA a, ptLLA b)
        {
            return new ptLLA(a.lat + b.lat, a.lng + b.lng, a.alt + b.alt);
        }
        public static ptLLA operator -(ptLLA a, ptLLA b)
        {
            return new ptLLA(a.lat - b.lat, a.lng - b.lng, a.alt - b.alt);
        }
        public override string ToString()
        {
            return "[" + lat.ToString("0.000000") + "; " + lng.ToString("0.000000") + "; " + alt.ToString("0.00") + "]";
        }

    }
    public class RPY
    {
        public double roll;
        public double pitch;
        public double yaw;
        public RPY() { }
        public RPY(double _roll, double _pitch, double _yaw)
        {
            roll = _roll;
            pitch = _pitch;
            yaw = _yaw;
        }
        public override string ToString()
        {
            return "[" + roll.ToString("0.00") + "; " + pitch.ToString("0.00") + "; " + yaw.ToString("0.00") + "]";
        }

    }
    public class HeadingSpeed
    {
        public double heading;   // degrees, true North = 0, clockwise
        public double speed;     // horizontal speed, m/s
        public double vd;        // vertical rate, m/s, positive = ascending (up)
                                 // NED Down axis = −vd; converted in LLA2NED()
        public HeadingSpeed() { }
        public HeadingSpeed(double _heading, double _speed)
        {
            heading = _heading;
            speed   = _speed;
        }
        public HeadingSpeed(double _heading, double _speed, double _vd)
        {
            heading = _heading;
            speed   = _speed;
            vd      = _vd;
        }
    }

    public class CUE
    {

        public enum IFFS
        {
            UNKNOWN = 0,
            FRIEND = 1,
            FOE = 2,
            IGNORE = 3,
        }

        public CUE() { }
        public string? ICAO { get; set; } = "";
        public string? Name { get; set; } = "";
        public double Range_m { get; set; } = 0;
        public double Bearing { get; set; } = 0;
        public double Elevation { get; set; } = 0;
        public ptLLA Position { get; set; } = new ptLLA();
        public HeadingSpeed HeadingSpeed { get; set; } = new HeadingSpeed();
        public TRACK_CLASSIFICATION Classification { get; set; } = TRACK_CLASSIFICATION.AC_MED;
        public IFFS IFF { get; set; } = IFFS.UNKNOWN;

        public void Set(DataGridViewRow dvgr)
        {
            ICAO = dvgr.Cells["ICAO"].Value.ToString();
            Name = dvgr.Cells["CS"].Value.ToString();
            Range_m = Convert.ToDouble(dvgr.Cells["Range"].Value) * 1000;
            Bearing = Convert.ToDouble(dvgr.Cells["Bearing"].Value);
            Elevation = Convert.ToDouble(dvgr.Cells["Elevation"].Value);
            Position = new ptLLA(Convert.ToDouble(dvgr.Cells["Latitude"].Value), Convert.ToDouble(dvgr.Cells["Longitude"].Value), Convert.ToDouble(dvgr.Cells["Altitude"].Value));
            HeadingSpeed = new HeadingSpeed(Convert.ToDouble(dvgr.Cells["Heading"].Value), Convert.ToDouble(dvgr.Cells["Speed"].Value));
            //Classification = string.IsNullOrEmpty( dvgr.Cells["Class"].Value.ToString())? TRACK_CLASSIFICATION.None: (TRACK_CLASSIFICATION)dvgr.Cells["Class"].Value.ToString();
            //Enum.TryParse(dvgr.Cells["Class"].Value.ToString(), out TRACK_CLASSIFICATION aClass);
            //Classification = aClass;
        }

        public override string ToString()
        {

            return $"ICAO = {ICAO}\nRange = {(Range_m / 1000).ToString("0.00")}km\nBearing = {Bearing.ToString("0.00")}°\n{Position.ToString()} ";
        }

    }

    public static class MyExtensionClass
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(nint hWnd, int wMsg, bool wParam, int lParam);

        private const int WM_SETREDRAW = 11;

        public static void SuspendDrawing(this Control ctrl)
        {
            var parent = ctrl.Parent;
            if (parent != null)
            {
                SendMessage(parent.Handle, WM_SETREDRAW, false, 0);
            }
        }

        public static void ResumeDrawing(this Control ctrl)
        {
            var parent = ctrl.Parent;
            if (parent != null)
            {
                SendMessage(parent.Handle, WM_SETREDRAW, true, 0);
                parent.Refresh();
            }
        }
    }



    [Serializable]
    public class bMarker : GMapMarker, ISerializable
    {

        public Bitmap? Bitmap { get; set; }

        [NonSerialized]
        public Brush Fill = new SolidBrush(Color.FromArgb(155, Color.Blue));

        public float Bearing = 0;
        private float scale = 1;

        public float Scale
        {
            get
            {
                return scale;
            }
            set
            {
                scale = value;

                Size = new Size((int)(14 * scale), (int)(14 * scale));
                Offset = new System.Drawing.Point(-Size.Width / 2, (int)(-Size.Height / 1.4));
            }
        }

        public bMarker(PointLatLng p)
           : base(p)
        {
            Scale = 1;
        }

        public bMarker(PointLatLng p, Bitmap b) : base(p)
        {
            Bitmap = b;
            Size = new Size(Bitmap.Width, Bitmap.Height);
            Offset = new System.Drawing.Point(-Size.Width / 2, -Size.Height / 2);
            //Scale = 1;
        }
        //static readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>();

        public override void OnRender(Graphics g)
        {

            //g.TranslateTransform(ToolTipPosition.X, ToolTipPosition.Y);
            var c = g.BeginContainer();
            {
                //g.RotateTransform(Bearing - Overlay.Control.Bearing);
                ////g.ScaleTransform(Scale, Scale);
                ////g.DrawImage(Bitmap, LocalPosition.X, LocalPosition.Y);
                //g.DrawImage(Bitmap, LocalPosition.X, LocalPosition.Y+ Size.Height/2);
                Bitmap bb2 = COMMON.RotateImage2(Bitmap, Bearing);
                g.DrawImage(bb2, LocalPosition.X, LocalPosition.Y); // handle offset outside so tooltips go with it

                bb2.Dispose();

            }
            g.EndContainer(c);
            //g.TranslateTransform(-ToolTipPosition.X, -ToolTipPosition.Y);

        }

        //public override void Dispose()
        //{
        //    if (Bitmap != null)
        //    {
        //        if (!iconCache.ContainsValue(Bitmap))
        //        {
        //            Bitmap.Dispose();
        //            Bitmap = null;
        //        }
        //    }

        //    base.Dispose();
        //}

        #region ISerializable Members

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            GetObjectData(info, context);
        }

        protected bMarker(SerializationInfo info, StreamingContext context)
           : base(info, context)
        {

        }

        #endregion


    }


}
