using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using AForge.Video;
using AForge.Imaging.Filters;
using SmartBorder.Classes.DB_Classes;

namespace SmartBorder.Classes.Cameras
{
    class BitmapsProcessing
    {
        // ------------------------------------------------------------------------------------------ - - - - - - - - - - CONST
        // The motion present that will be ignored if the motion was more than it , "To much motion"
        const double IGNORE_VAL = 0.3;
        // ------------------------------------------------------------------------------------------ - - - - - - - - - -

        // The old bitmap frame
        private Bitmap OldBmp;
        // The new bitmap frame
        private Bitmap NewBmp;
        // The event when there is motion with the setting !
        public event EventHandler MotionDetected;
        // The motion rectangles
        public Rectangle[] myRectangles { get; private set; }
        // The camera moiton alert setting
        public CameraAlertSetting myCameraAlertSetting { get; set; }
        // Basic setting of the bitmap
        private int Width;
        private int Height;
        

        /// <summary>
        /// Constructor , Takes webcam Alertsetting
        /// </summary>
        /// <param name="AlertSettings">The alert setting of camera</param>
        public BitmapsProcessing(CameraAlertSetting AlertSettings)
        {
            // Load the camera alert setting
            myCameraAlertSetting = AlertSettings;
            OldBmp = NewBmp = null;
        }
        /// <summary>
        /// Camera newframe handler, will be used to calculate the motion recangles
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs">The new Frame form the webcam</param>
        public void NewFrame_Handler(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Get the new frame fromt he camera
                Bitmap newFrame = (Bitmap)eventArgs.Frame.Clone();
                Grayscale filter = new Grayscale(0.2125, 0.7154, 0.0721);
                // if this is the first frame
                if (OldBmp == null)
                {
                    OldBmp = filter.Apply(newFrame);
                    Width = OldBmp.Width;
                    Height = OldBmp.Height;
                    myRectangles = new Rectangle[0];
                }
                // Process the two images
                NewBmp = filter.Apply(newFrame);
                CalcRectangles();
                // save the newFrame as oldFrame 
                OldBmp = NewBmp;
            }
            catch (Exception) { }
        }        
        /// <summary>
        /// Camera newframe handler, will be used to calculate the motion recangles in the alarm setting
        /// Will trigger the MotionDetected event ! on any motion in the AlertSetting settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs">The new Frame form the webcam</param>
        public void NewFrame_Alert_Handler(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Check if the AlertSetting is active
                if (myCameraAlertSetting.isActive == false)
                    return;
                // Get the new frame fromt he camera
                Bitmap newFrame = (Bitmap)eventArgs.Frame.Clone();
                Grayscale filter = new Grayscale(0.2125, 0.7154, 0.0721);
                // if this is the first frame
                if (OldBmp == null)
                {
                    OldBmp = filter.Apply(newFrame);
                    Width = OldBmp.Width;
                    Height = OldBmp.Height;
                    myRectangles = new Rectangle[0];
                }
                // Process the two images
                NewBmp = filter.Apply(newFrame);
                // Check if it's vaild time to do image processing
                int hh = System.DateTime.Now.Hour;
                int mm = System.DateTime.Now.Minute;
                if (myCameraAlertSetting.TimeLines.Any(t => hh >= t.From.Hours && hh <= t.To.Hours && mm >= t.From.Minutes && mm <= t.To.Minutes))
                {
                    CalcRectangles();
                    // Check the rectangles in the AlertSetting settings
                    if (myCameraAlertSetting is GridCameraAlertSetting)
                    {
                        // it is -------------------------  GridCameraAlertSetting   ------------------------------------------ 
                        GridCameraAlertSetting settings = (GridCameraAlertSetting)myCameraAlertSetting;
                        // Calculate the rectangle dementions
                        int xPlus = Width / settings.MyMatrix.GetLength(0);
                        int yPlus = Height / settings.MyMatrix.GetLength(1);
                        for (int i = 0; i < settings.MyMatrix.GetLength(0); i++)
                            for (int j = 0; j < settings.MyMatrix.GetLength(1); j++)
                                // Draw the rectangle if the user selected it
                                if (settings.MyMatrix[i, j])
                                {
                                    // If this rectangle is in the motion section , we will check it
                                    Rectangle settingRectangle = new Rectangle(i * xPlus, j * yPlus, xPlus, yPlus);
                                    for (int k = 0; k < myRectangles.Count(); k++)
                                        if (settingRectangle.IntersectsWith(myRectangles[k]))
                                        {
                                            // We got Motion detection !
                                            // Active the Motion detecte !
                                            MotionDetected(this, EventArgs.Empty);
                                            OldBmp = NewBmp = null;
                                            break;
                                        }
                                }
                    }
                    else
                    {   
                        // it is -------------------------  LineCameraAlertSetting   ------------------------------------------
                        LineCameraAlertSetting settings = (LineCameraAlertSetting)myCameraAlertSetting;
                        // Check if any line intersect with any of the rectangles
                        for (int i = 0; i < settings.Lines.Count; i++)
                            for (int j = 0; j < myRectangles.Count(); j++)
                                if (settings.Lines[i].IntersectsWith(myRectangles[j]))
                                {
                                    // We got Motion detection !
                                    // Active the Motion detecte !
                                    MotionDetected(this, EventArgs.Empty);
                                    OldBmp = NewBmp = null;
                                    break;
                                }
                    }
                }
                // Save the newFrame as oldFrame
                OldBmp = NewBmp;
            }
            catch (Exception err) {
#if DEBUG
                throw err;
#endif
            }
        }

        // images processing
        private void CalcRectangles()
        {
            if (OldBmp.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new FormatException("Only 8bppIndexed format supported !");
            if (NewBmp.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new FormatException("Only 8bppIndexed format supported !");
            BitmapData oldData = OldBmp.LockBits(new Rectangle(0, 0, OldBmp.Width, OldBmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            BitmapData newData = NewBmp.LockBits(new Rectangle(0, 0, NewBmp.Width, NewBmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            // Get the address of the first line.
            IntPtr ptr = newData.Scan0;
            IntPtr ptrOld = oldData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(newData.Stride) * NewBmp.Height;
            byte[] ValuesNew = new byte[bytes];
            int bytesOld = Math.Abs(oldData.Stride) * OldBmp.Height;
            byte[] ValuesOld = new byte[bytesOld];

            // Copy the values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, ValuesNew, 0, bytes);
            System.Runtime.InteropServices.Marshal.Copy(ptrOld, ValuesOld, 0, bytesOld);

            int[] CmpArr = new int[ValuesNew.Length];
            int Counter = 0;
            for (int i = 1; i < ValuesNew.Length; i += 1)
            {
                if ((ValuesNew[i] > ValuesOld[i] + myCameraAlertSetting.MotionSensitive))//Motion is lighter
                    CmpArr[Counter++] = i;
                else if ((ValuesNew[i] < ValuesOld[i] - myCameraAlertSetting.MotionSensitive))//Motion is darker
                    CmpArr[Counter++] = i;
            }

            // Copy the values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(ValuesNew, 0, ptr, bytes);
            System.Runtime.InteropServices.Marshal.Copy(ValuesOld, 0, ptrOld, bytesOld);

            // Done image proccesing
            NewBmp.UnlockBits(newData);
            OldBmp.UnlockBits(oldData);

            // If thers is alot of motion , more than 30% for example of the pixels of the image is detected as motion
            // So it's could be from a light or camera movements or strange activety that we cant define it as alert
            if (Counter > IGNORE_VAL * (Width * Height))
                return;

            // Convert all points to ShapeObjects
            List<MyShape> MyShp = new List<MyShape>();
            for (int i = 0; i < Counter; i++)
            {
                int PntX = CmpArr[i] % Width;
                int PntY = CmpArr[i] / Width;
                double dxZero = PntX;
                double dyZero = PntY;
                double dxWidth = Width - PntX;

                double DisZero = Math.Sqrt(dxZero * dxZero + dyZero * dyZero);
                double DisWidth = Math.Sqrt(dxWidth * dxWidth + dyZero * dyZero);
                int MyShpSize = MyShp.Count;
                int j = 0;
                for (j = 0; j < MyShpSize; j++)
                    if (MyShp[j].InRange(DisZero, DisWidth))
                    {
                        // add mypoint to existing Shape that we found in range
                        MyShp[j].AddPoint(new MyPoint(PntX, PntY, DisZero, DisWidth));
                        break;
                    }
                if (j >= MyShpSize)
                {// Adding new Shape for our new mypoint
                    MyShp.Add(new MyShape(new MyPoint(PntX, PntY, DisZero, DisWidth)));
                }// End Conveart points to ShapeObject
            }
            // We got all Shapes in MyShp ready for next step

            myRectangles = new Rectangle[MyShp.Count];
            for (int i = 0; i < MyShp.Count; i++)
                myRectangles[i] = MyShp[i].GetRectangle();
        }

    }


    /// <summary>
    /// A point that contains a regular point and the distance of this point from the zero and from the width point
    /// </summary>
    class MyPoint
    {
        // The point
        public System.Drawing.Point Pnt;
        // Distance from the zero 
        public double DistZero;
        // Distance from the width
        public double DistWidth;
        /// <summary>
        /// Constractor of the MyPoint
        /// </summary>
        /// <param name="x">The x of the point</param>
        /// <param name="y">The y of the point</param>
        /// <param name="distanceZero">The distance from the zero</param>
        /// <param name="distanceWidth">The distance from the width</param>
        public MyPoint(int x, int y, Double distanceZero, Double distanceWidth)
        {
            Pnt = new System.Drawing.Point(x, y);
            DistZero = distanceZero;
            DistWidth = distanceWidth;
        }

    }

    /// <summary>
    /// The Shape that will contains the points of the shape and the limits of the shape
    /// </summary>
    class MyShape
    {
        //------------------------------------------------------------------------------------------- - - - - - - - - - CONST
        // That will define if the two points are closed inuf to be in the same shape
        const int PIXELS_RADIUS = 10;
        //------------------------------------------------------------------------------------------- - - - - - - - - -
        // The low distance from zero point
        public double minZero { get; set; }
        // The height distance from zero point
        public double maxZero { get; set; }
        // The low distance from width point
        public double minWidth { get; set; }
        // The height distance from width point
        public double maxWidth { get; set; }
        // The points of this shape
        public List<System.Drawing.Point> ObjectPoints;

        /// <summary>
        /// Shape constractur
        /// </summary>
        /// <param name="StartPoint">The starting point of this shape</param>
        public MyShape(MyPoint StartPoint)
        {
            // Create a new points of the page
            ObjectPoints = new List<System.Drawing.Point>();
            ObjectPoints.Add(StartPoint.Pnt);
            // Calculate of the shape distance radious
            minZero = StartPoint.DistZero - PIXELS_RADIUS;
            maxZero = StartPoint.DistZero + PIXELS_RADIUS;
            minWidth = StartPoint.DistWidth - PIXELS_RADIUS;
            maxWidth = StartPoint.DistWidth + PIXELS_RADIUS;
        }
        /// <summary>
        /// Check if an another point distance is in the same range of this shape
        /// </summary>
        /// <param name="DisZero">The distance from zero point</param>
        /// <param name="DisWidth">The distance from width point</param>
        /// <returns>True --- In the same range ; False --- Not in range</returns>
        public bool InRange(double DisZero, double DisWidth)
        {
            // Check distance bounds
            if (DisZero > maxZero || DisZero < minZero || DisWidth > maxWidth || DisWidth < minWidth)
                return false;
            return true;
        }
        /// <summary>
        /// Add another point to this shape
        /// </summary>
        /// <param name="Mypoint">The point that will be added to the shape</param>
        public void AddPoint(MyPoint Mypoint)
        {
            // Adding the point
            ObjectPoints.Add(Mypoint.Pnt);
            // Check if this point distance is bigger or smaller , Will update the maximum or minmum distance
            if (Mypoint.DistZero + PIXELS_RADIUS > maxZero)
                maxZero = Mypoint.DistZero + PIXELS_RADIUS;
            else
                minZero = Mypoint.DistZero - PIXELS_RADIUS;
            if (Mypoint.DistWidth + PIXELS_RADIUS > maxWidth)
                maxWidth = Mypoint.DistWidth + PIXELS_RADIUS;
            else
                minWidth = Mypoint.DistWidth - PIXELS_RADIUS;
        }
        /// <summary>
        /// Draw a rectangle that restricts the shape 
        /// </summary>
        /// <returns>The rectangle of this shape</returns>
        public Rectangle GetRectangle()
        {
            int minx = 9999999, miny = 9999999, maxx = 0, maxy = 0, i;
            // Take the maximum point and the smaller point of the shape for the rectangle
            for (i = 0; i < ObjectPoints.Count; i++)
            {
                if (ObjectPoints[i].X > maxx)
                    maxx = ObjectPoints[i].X;
                else if (ObjectPoints[i].X < minx)
                    minx = ObjectPoints[i].X;
                if (ObjectPoints[i].Y > maxy)
                    maxy = ObjectPoints[i].Y;
                else if (ObjectPoints[i].Y < miny)
                    miny = ObjectPoints[i].Y;
            }
            // Return the Rectangle
            return new Rectangle(minx, miny, maxx - minx, maxy - miny);
        }
    }
}
