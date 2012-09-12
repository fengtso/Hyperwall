using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;

using System.Drawing;

using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using Emgu.Util;
using Emgu.CV.CvEnum;
using Emgu.CV.ML;
//using Microsoft.Research.Kinect.Nui;
//using Microsoft.Research.Kinect.Audio;
using Microsoft.Kinect;
using System.Runtime.InteropServices;

namespace HandTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            socket = new HyperwallKinectClientSocket();
            socket.connect();



            //init_KNN();
        }


       

        HyperwallKinectClientSocket socket;
        

        //nearer than 800mm the returned depth value will be 0
        const int min_depth_range = 800; //800mm
        const int max_depth_range = 2000; //4000mm

        int zoom_in_counter = 0;
        int zoom_out_counter = 0;
        int click_counter = 0;

        bool isNaviModeOn = false;
        bool isFirstTouch = true;

        int naviModeCounter = 0;
        int naviModeThreshold = 25;
        string commandMode = "";
        //Lock button position
        PointF lockButtonPosition = new PointF(40.0f, 240.0f);
        int lockButtonWidth = 80;
        int lockButtonHeight = 50;

        Image<Bgr, Byte> currentColorFrame;
        Image<Gray, Byte> currentDepthFrame;
        Image<Bgr, Byte> CMULogoImage;

        //KNN parameters
        //palm/fist knn classifier
        int trainSampleCount = 200;
        Matrix<float> trainData;
        Matrix<float> trainClasses;
        Matrix<float> testSample;
        KNearest knn;
        int K = 3;
        Matrix<float> results, neighborResponses;

        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];


        Dictionary<JointType, System.Windows.Media.Brush> jointColors = new Dictionary<JointType, System.Windows.Media.Brush>() { 
            {JointType.HipCenter, new SolidColorBrush(System.Windows.Media.Color.FromRgb(169, 176, 155))},
            {JointType.Spine, new SolidColorBrush(System.Windows.Media.Color.FromRgb(169, 176, 155))},
            {JointType.ShoulderCenter, new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 230, 29))},
            {JointType.Head, new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 0,   0))},
            {JointType.ShoulderLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(79,  84,  33))},
            {JointType.ElbowLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(84,  33,  42))},
            {JointType.WristLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 126, 0))},
            {JointType.HandLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(215,  86, 0))},
            {JointType.ShoulderRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(33,  79,  84))},
            {JointType.ElbowRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(33,  33,  84))},
            {JointType.WristRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(77,  109, 243))},
            {JointType.HandRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(37,   69, 243))},
            {JointType.HipLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(77,  109, 243))},
            {JointType.KneeLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(69,  33,  84))},
            {JointType.AnkleLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 170, 122))},
            {JointType.FootLeft, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 126, 0))},
            {JointType.HipRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(181, 165, 213))},
            {JointType.KneeRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 222,  76))},
            {JointType.AnkleRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 228, 156))},
            {JointType.FootRight, new SolidColorBrush(System.Windows.Media.Color.FromRgb(77,  109, 243))}
        };

        /// <summary>
        /// Delete a GDI object
        /// </summary>
        /// <param name="o">The poniter to the GDI object to be deleted</param>
        /// <returns></returns>
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
        /// </summary>
        /// <param name="image">The Emgu CV Image</param>
        /// <returns>The equivalent BitmapSource</returns>
        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }

        KinectSensor nui;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //nui = new Runtime();
            //nui = Runtime.Kinects[0];
            nui = KinectSensor.KinectSensors[0];

            if (null != this.nui)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.nui.SkeletonStream.Enable();
                this.nui.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.nui.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);

                // Add an event handler to be called whenever there is new color frame data
                this.nui.SkeletonFrameReady += this.nui_showAugmentedColorFrame;

                // Start the sensor!
                try
                {
                    this.nui.Start();
                }
                catch (IOException)
                {
                    this.nui = null;
                }
            }


                //try
                //{
                //    nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
                //}
                //catch (InvalidOperationException)
                //{
                //    System.Windows.MessageBox.Show("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                //    return;
                //}

                //try
                //{
                //    nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                //    nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);

                //}
                //catch (InvalidOperationException)
                //{
                //    System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                //    return;
                //}
            
            //adjust the elavation angle (degree)
            this.nui.ElevationAngle = -15;

            //nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_showAugmentedColorFrame);    
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            this.nui.Stop();
        }

        byte[] convertColorFrame(byte[] colorFrame, int width, int height)
        {
            byte[] colorFrame3Channel = new byte[width * height * 3];

            for (int heightIdx = 0; heightIdx < height; heightIdx++)
            {
                for (int widthIdx = 0; widthIdx < width; widthIdx++)
                {
                    colorFrame3Channel[(heightIdx * width + widthIdx) * 3] = colorFrame[(heightIdx * width + widthIdx) * 4];
                    colorFrame3Channel[(heightIdx * width + widthIdx) * 3 + 1] = colorFrame[(heightIdx * width + widthIdx) * 4 + 1];
                    colorFrame3Channel[(heightIdx * width + widthIdx) * 3 + 2] = colorFrame[(heightIdx * width + widthIdx) * 4 + 2];
                }
            }

            return colorFrame3Channel;
        }

        //load knn training data
        private void init_KNN()
        {
            String folder = "C:\\KinectImage\\rightHand\\palm_small";
            String[] filePaths = Directory.GetFiles(folder);
            int BWImageWidth = 50;
            trainData = new Matrix<float>(trainSampleCount, BWImageWidth * BWImageWidth);
            trainClasses = new Matrix<float>(trainSampleCount, 1);

            int fileIdx = 0;
            int sampleIdx = 0;
            for (fileIdx = 0; fileIdx < filePaths.Length; fileIdx++)
            {
                Image<Gray, Byte> img = new Image<Gray, Byte>(filePaths[fileIdx]);

                for (int rowIdx = 0; rowIdx < BWImageWidth; rowIdx++)
                {
                    for (int colIdx = 0; colIdx < BWImageWidth; colIdx++)
                    {
                        //Console.WriteLine(filePaths[fileIdx]);
                        trainData[sampleIdx, BWImageWidth * rowIdx + colIdx] = (float)img[rowIdx, colIdx].MCvScalar.v0;
                        trainClasses[sampleIdx, 0] = 0.0f;
                        //Console.WriteLine((float)img[rowIdx, colIdx].MCvScalar.v0);
                    }
                }
                sampleIdx++;
            }

            //read fist
            folder = "C:\\KinectImage\\rightHand\\fist_small";
            filePaths = Directory.GetFiles(folder);
            for (fileIdx = 0; fileIdx < filePaths.Length; fileIdx++)
            {

                Image<Gray, Byte> img = new Image<Gray, Byte>(filePaths[fileIdx]);

                for (int rowIdx = 0; rowIdx < BWImageWidth; rowIdx++)
                {
                    for (int colIdx = 0; colIdx < BWImageWidth; colIdx++)
                    {
                        //Console.WriteLine(filePaths[fileIdx]);
                        trainData[sampleIdx, BWImageWidth * rowIdx + colIdx] = (float)img[rowIdx, colIdx].MCvScalar.v0;
                        trainClasses[sampleIdx, 0] = 1.0f;
                        //Console.WriteLine((float)img[rowIdx, colIdx].MCvScalar.v0);
                    }
                }
                sampleIdx++;
            }

            testSample = new Matrix<float>(1, BWImageWidth * BWImageWidth);
            results = new Matrix<float>(testSample.Rows, 1);
            neighborResponses = new Matrix<float>(testSample.Rows, K);
            knn = new KNearest(trainData, trainClasses, null, false, K);

            /*
            //testing
            folder = "C:\\KinectImage\\rightHand\\palm_small";
            filePaths = Directory.GetFiles(folder);

            for (fileIdx = 0; fileIdx < filePaths.Length; fileIdx++)
            {

                Image<Gray, Byte> img = new Image<Gray, Byte>(filePaths[fileIdx]);

                for (int rowIdx = 0; rowIdx < BWImageWidth; rowIdx++)
                {
                    for (int colIdx = 0; colIdx < BWImageWidth; colIdx++)
                    {
                         testSample[0, rowIdx*BWImageWidth + colIdx] = (float)img[rowIdx, colIdx].MCvScalar.v0;              
                    }
                }
                float response = knn.FindNearest(testSample, K, results, null, neighborResponses, null);
                Console.WriteLine(response);
            }
             * */
            
        }


        private void FromShort(short number, out byte byte1, out byte byte2)
        {
            byte2 = (byte)(number >> 8);
            byte1 = (byte)(number & 255);
        }

        private System.Windows.Point getDisplayPositionInTestImage(Joint joint, out int depthInMillimeter)
        {

            float depthX, depthY;
            short depthZ;
            DepthImageFrame depthFrame = this.nui.DepthStream.OpenNextFrame(10);
            DepthImagePoint depthPoint = this.nui.MapSkeletonPointToDepth(joint.Position, DepthImageFormat.Resolution320x240Fps30);
            
            //nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY, out depthZ);

            //convert 2-byte short into millimeter
            /*
            byte byte1, byte2;
            FromShort(depthZ, out byte1, out byte2);
            depthInMillimeter = (int)(byte1 >> 3 | byte2 << 5);
            */
            depthInMillimeter = depthPoint.Depth;
            //Console.WriteLine(depthInMillimeter);

            depthX = depthPoint.X * 320; //convert to 320, 240 space
            depthY = depthPoint.Y * 240; //convert to 320, 240 space
            int colorX, colorY;
            //ImageViewArea iv = new ImageViewArea();
            //// only ImageResolution.Resolution640x480 is supported at this point
            //nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);
            ColorImagePoint colorPoint = depthFrame.MapToColorImagePoint(depthPoint.X, depthPoint.Y, this.nui.ColorStream.Format);

            colorX = colorPoint.X;
            colorY = colorPoint.Y;

            // map back to TestImage.Width & TestImage.Height
            return new System.Windows.Point((int)((float)currentColorFrame.Width * colorX / 640.0f), (int)((float)currentColorFrame.Height * colorY / 480.0f));
        }

        System.Windows.Media.PointCollection getJointPositionsAndDepths(out List<int> depthList, Microsoft.Kinect.JointCollection joints, params JointType[] ids)
        {
            System.Windows.Media.PointCollection points = new System.Windows.Media.PointCollection(ids.Length);
            depthList = new List<int>();

            for (int i = 0; i < ids.Length; ++i)
            {
                int depthZ;
                points.Add(getDisplayPositionInTestImage(joints[ids[i]], out depthZ));
                depthList.Add(depthZ);
            }

            return points;
        }

        void checkNaviMode(System.Windows.Media.PointCollection jointPositions, List<int> depthList)
        {
            //extract lefhand info
            System.Windows.Point leftHandPoint = jointPositions.ElementAt(0);
            int leftHandDepth = depthList.ElementAt(0);

            //handle unlock action (sliding)
            if (!isNaviModeOn && isFirstTouch)
            {
                if (leftHandPoint.X > lockButtonPosition.X - lockButtonWidth / 2 &&
                leftHandPoint.X < lockButtonPosition.X + lockButtonWidth / 2 &&
                leftHandPoint.Y > lockButtonPosition.Y - lockButtonHeight / 2 &&
                leftHandPoint.Y < lockButtonPosition.Y + lockButtonHeight / 2)
                {
                    isFirstTouch = false;

                    //update button x axis
                    lockButtonPosition.X = (float)leftHandPoint.X;
                }
            
            }
            else if (!isNaviModeOn && !isFirstTouch){
                if (leftHandPoint.Y > lockButtonPosition.Y - (lockButtonHeight / 2  + 20)&&
                leftHandPoint.Y < lockButtonPosition.Y + (lockButtonHeight / 2 + 20))
                {
                    //update button x axis
                    lockButtonPosition.X = (float)leftHandPoint.X;
                    if (lockButtonPosition.X > 320)
                    { //swipe to center and unlock
                        isNaviModeOn = true;
                        isFirstTouch = true;
                        lockButtonPosition.X = 40;
                    }
                }
                else { //back to original position
                    isFirstTouch = true;
                    lockButtonPosition.X = 40;
                }
            }

            if(isNaviModeOn){
                if (leftHandPoint.X > lockButtonPosition.X - lockButtonWidth / 2 &&
                    leftHandPoint.X < lockButtonPosition.X + lockButtonWidth / 2 &&
                    leftHandPoint.Y > lockButtonPosition.Y - lockButtonHeight / 2 &&
                    leftHandPoint.Y < lockButtonPosition.Y + lockButtonHeight / 2)
                {
                    naviModeCounter++;
                    if (naviModeCounter > naviModeThreshold)
                    {
                        isNaviModeOn = false;
                        naviModeCounter = 0;
                    }
                }
                else {
                    naviModeCounter = 0;
                }
            }
            
        }

        void interpretCommand(System.Windows.Media.PointCollection jointPositions, List<int> depthList)
        { 
            //only take care of right hand first
            System.Windows.Point rightHandPoint = jointPositions.ElementAt(1);
            int rightHandDepth = depthList.ElementAt(1);
            
            System.Windows.Point leftHandPoint = jointPositions.ElementAt(0);
            int leftHandDepth = depthList.ElementAt(0);

            string textInString = "";
            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 0.8, 0.8);

            double x_ratio = rightHandPoint.X / (double)currentColorFrame.Width;
            double y_ratio = rightHandPoint.Y / (double)currentColorFrame.Height;

            //distance range
            int zoom_min = 1000;
            int zoom_max = 1700;
            int zoom_buffer_count = 25;
            float radius = 25.0f;

            int click_buffer_count = 25;

            if (rightHandDepth > zoom_min && rightHandDepth < zoom_max) //airmouse
            {
                zoom_in_counter = 0;
                zoom_out_counter = 0;
           
                //textInString = "AirMouse";
                //commandMode = "AirMouse";
                
                
                //check if click action is activated (i.e., left-hand position is close to right-hand)
                if (Math.Abs(rightHandPoint.X - leftHandPoint.X) < 20 && Math.Abs(rightHandPoint.Y - leftHandPoint.Y) < 20) {
                    click_counter++;
                    
                    if (click_counter > click_buffer_count)
                    {
                        textInString = "Click";
                        commandMode = "MOUSE_CLICK";
                        socket.kinectClick();
                        click_counter = 0;
                    }
                }
                else{
                    click_counter = 0;
                    textInString = "AirMouse";
                    commandMode = "AirMouse";
                    socket.kinectMove(x_ratio, y_ratio);
                }
                /*
                double x = rightHandPoint.X - center_width;
                double y = rightHandPoint.Y - center_height;
                double distance = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                double radians = Math.Atan(y / x);
                double degree = (radians / Math.PI) * 180;
                degree = -degree;

                if (x < 0)
                {
                    degree = degree + 180;
                }
                else if (x > 0 && y > 0)
                {
                    degree = degree + 360;
                }

                textInString = "dist:" + Math.Round(distance).ToString() + " deg:" + Math.Round(degree).ToString() + " depth:" + rightHandDepth.ToString();

                socket.airMouse(degree, distance);
                */
                //socket.kinectMove(x_ratio, y_ratio);

            }
            else if (rightHandDepth < zoom_min) //zoom_in
            {
                radius = 40.0f;
                socket.stopAirmouse();
                zoom_out_counter = 0;
                zoom_in_counter++;
                commandMode = "ZoomIn";
                textInString = "ZoomIn";

                if (zoom_in_counter > zoom_buffer_count)
                {
                    socket.zoomIn();
                    zoom_in_counter = 0;                
                }
  
            }
            else if (rightHandDepth > zoom_max){ //zoom_out
                radius = 15.0f;
                socket.stopAirmouse();
                zoom_in_counter = 0;
                zoom_out_counter++;
                commandMode = "ZoomOut";
                textInString = "ZoomOut";

                if (zoom_out_counter > zoom_buffer_count)
                {
                    socket.zoomOut();
                    zoom_out_counter = 0;
                    
                }
            }

            /*
            //check if it is in green zone for ZOOM_IN/ZOOM_OUT
            if (rightHandPoint.X < center_width + margin_right &&
                rightHandPoint.X > center_width - margin_left &&
                rightHandPoint.Y < center_height + margin_down &&
                rightHandPoint.Y > center_height - margin_up)
            {
                socket.stopAirmouse();
                
                //zoom-in/zoom-out mode
                if (rightHandDepth < zoom_in_max && rightHandDepth > zoom_in_min) {
                    textInString = "ZOOM_IN:" + rightHandDepth.ToString();
                    zoom_out_counter = 0;
                    zoom_in_counter++;
                    if (zoom_in_counter > zoom_buffer_count)
                    {
                        socket.zoomIn();
                        zoom_in_counter = 0;
                    }
                    
                }
                else if (rightHandDepth < zoom_out_min && rightHandDepth > zoom_in_max) {
                    textInString = "NONE:" + rightHandDepth.ToString();
                }
                else if (rightHandDepth < zoom_out_max && rightHandDepth > zoom_out_min)
                {
                    textInString = "ZOOM_OUT:" + rightHandDepth.ToString();
                    zoom_in_counter = 0;
                    zoom_out_counter++;
                    if (zoom_out_counter > zoom_buffer_count)
                    {
                        socket.zoomOut();
                        zoom_out_counter = 0;
                    }
                }
                else {
                    textInString = "NONE:" + rightHandDepth.ToString();
                }

            }
            else {
                //calcuate the distance between center and angle degree

                double x = rightHandPoint.X - center_width;
                double y = rightHandPoint.Y - center_height;
                double distance = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                double radians = Math.Atan(y/x);
                double degree = (radians / Math.PI) * 180;
                degree = -degree;
                
                if (x < 0) {
                    degree = degree + 180;
                }
                else if (x > 0 && y > 0) {
                    degree = degree + 360;
                }
                
                textInString = "dist:" + Math.Round(distance).ToString() + " deg:" + Math.Round(degree).ToString() + " depth:" + rightHandDepth.ToString();
                
                socket.airMouse(degree, distance);
            }
            */
            //overaly right hand
            Bgr bgr; 
            /*
            if(commandMode == "AirMouse")
                bgr = new Bgr(200, 200, 200); //Red for hands
            else if(commandMode == "ZoomIn")
                bgr = new Bgr(100, 0, 100); //Red for hands
            else if(commandMode == "ZoomOut")
                bgr = new Bgr(0, 200, 0); //Red for hands
            else
                bgr = new Bgr(0, 0, 255); //Red for hands
            */
            bgr = new Bgr(100, 0, 100);

            //convert point type
            System.Drawing.PointF point = new System.Drawing.PointF((float)rightHandPoint.X, (float)rightHandPoint.Y);
            CircleF circle = new CircleF();
            circle.Center = point;
            
            //set radius/stroke according to the depth
            float ratio = (float)(max_depth_range - rightHandDepth) / (float)(max_depth_range - min_depth_range);
            if (ratio < 0)
                ratio = 0;
            
            //float radius = 40.0f * ratio;
            
            int stroke = (int)(40.0f * ratio);
            circle.Radius = radius;
            currentColorFrame.Draw(circle, bgr, -1);

            //System.Drawing.Point textLoc = new System.Drawing.Point((int)rightHandPoint.X, (int)rightHandPoint.Y);
            System.Drawing.Point textLoc = new System.Drawing.Point(300, 30);
            currentColorFrame.Draw(textInString, ref font, textLoc, new Bgr(0, 0, 0));

        }

        void drawJointPositions(System.Windows.Media.PointCollection jointPositions, List<int> depthList)
        {
            int jointIdx = 0;
            foreach (System.Windows.Point onePoint in jointPositions)
            {
                Bgr bgr;
                //if (jointIdx % 3 == 0)
                //  bgr = new Bgr(255, 0, 0); //Blue for head
                //else 
                bgr = new Bgr(100, 100, 100); //Red for hands

                //convert point type
                System.Drawing.PointF point = new System.Drawing.PointF((float)onePoint.X, (float)onePoint.Y);
                CircleF circle = new CircleF();
                circle.Center = point;

                //set radius/stroke according to the depth
                float ratio = (float)(max_depth_range - depthList.ElementAt(jointIdx)) / (float)(max_depth_range - min_depth_range);
                if (ratio < 0)
                    ratio = 0;

                float radius = 40.0f * ratio;
                int stroke = (int)(40.0f * ratio);
                circle.Radius = radius;
                currentColorFrame.Draw(circle, bgr, -1);

                MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 0.5, 0.5);
                string depthInString = depthList.ElementAt(jointIdx).ToString() + "mm";
                System.Drawing.Point textLoc = new System.Drawing.Point((int)onePoint.X, (int)onePoint.Y);
                currentColorFrame.Draw(depthInString, ref font, textLoc, new Bgr(0, 0, 0));

                jointIdx++;
            }
        }

        void drawZoomRectangle(System.Windows.Media.PointCollection jointPositions)
        {
            //find LeftTop and RightBottom points
            System.Drawing.Point leftTopPt = new System.Drawing.Point(640, 480);
            System.Drawing.Point rightBottomPt = new System.Drawing.Point(0, 0);

            foreach (System.Windows.Point onePoint in jointPositions)
            {
                if (onePoint.X > rightBottomPt.X)
                    rightBottomPt.X = (int)onePoint.X;

                if (onePoint.Y > rightBottomPt.Y)
                    rightBottomPt.Y = (int)onePoint.Y;

                if (onePoint.X < leftTopPt.X)
                    leftTopPt.X = (int)onePoint.X;

                if (onePoint.Y < leftTopPt.Y)
                    leftTopPt.Y = (int)onePoint.Y;
            }

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle();
            rect.Location = leftTopPt;
            rect.Width = rightBottomPt.X - leftTopPt.X;
            rect.Height = rightBottomPt.Y - leftTopPt.Y;

            currentColorFrame.Draw(rect, new Bgr(255, 0, 0), 5);
        }

        void overlayControlPanel() //overlay a control UI on the left-top
        {

            //MCvScalar pixel = CvInvoke.cvGet2D(currentColorFrame, 320, 240);

            MCvScalar S = new MCvScalar(0.5, 0.5, 0.5, 0.5);
            MCvScalar D = new MCvScalar(0.5, 0.5, 0.5, 0.5);

            string textInString = "";
            MCvFont font = new MCvFont(Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_DUPLEX, 0.8, 0.8);
            System.Drawing.Point textLoc;
            
            //overlay centered Green Rectangle
            MCvScalar overColor;
            if (isNaviModeOn)
            {
                textLoc = new System.Drawing.Point((int)lockButtonPosition.X -32, (int)lockButtonPosition.Y + 7);
                overColor = new MCvScalar(0, 255, 0, 0);
                textInString = "lock";
            }
            else
            {
                textLoc = new System.Drawing.Point((int)lockButtonPosition.X - 10, (int)lockButtonPosition.Y + 7);
                overColor = new MCvScalar(0, 0, 255, 0);
                textInString = "-> slide to unlock";
            }
            
            
            //overlay trail
            if (!isNaviModeOn) {
                MCvScalar trailColor = new MCvScalar(50, 50, 50, 0);
                for (int heightIdx = 240 - lockButtonHeight / 2; heightIdx < 240 + lockButtonHeight / 2; heightIdx++)
                {
                    for (int widthIdx = lockButtonWidth; widthIdx < 280; widthIdx++)
                    {
                        if (heightIdx > 0 && heightIdx < currentColorFrame.Height && widthIdx > 0 && widthIdx < currentColorFrame.Width)
                        {
                            MCvScalar source = CvInvoke.cvGet2D(currentColorFrame, heightIdx, widthIdx);

                            MCvScalar mergedPixel = new MCvScalar();
                            mergedPixel.v0 = (S.v0 * source.v0 + D.v0 * trailColor.v0);
                            mergedPixel.v1 = (S.v1 * source.v1 + D.v1 * trailColor.v1);
                            mergedPixel.v2 = (S.v2 * source.v2 + D.v2 * trailColor.v2);
                            mergedPixel.v3 = (S.v3 * source.v3 + D.v3 * trailColor.v3);

                            CvInvoke.cvSet2D(currentColorFrame, heightIdx, widthIdx, mergedPixel);
                        }
                    }
                }
            }
            
            for (int heightIdx = (int)lockButtonPosition.Y - lockButtonHeight / 2; heightIdx < (int)lockButtonPosition.Y + lockButtonHeight / 2; heightIdx++)
            {
                for (int widthIdx = (int)lockButtonPosition.X - lockButtonWidth / 2; widthIdx < (int)lockButtonPosition.X + lockButtonWidth / 2; widthIdx++)
                {
                    if (heightIdx > 0 && heightIdx < currentColorFrame.Height && widthIdx > 0 && widthIdx < currentColorFrame.Width)
                    {
                        MCvScalar source = CvInvoke.cvGet2D(currentColorFrame, heightIdx, widthIdx);

                        MCvScalar mergedPixel = new MCvScalar();
                        mergedPixel.v0 = (S.v0 * source.v0 + D.v0 * overColor.v0);
                        mergedPixel.v1 = (S.v1 * source.v1 + D.v1 * overColor.v1);
                        mergedPixel.v2 = (S.v2 * source.v2 + D.v2 * overColor.v2);
                        mergedPixel.v3 = (S.v3 * source.v3 + D.v3 * overColor.v3);

                        CvInvoke.cvSet2D(currentColorFrame, heightIdx, widthIdx, mergedPixel);
                    }
                }
            }

            //put text on the control button
            currentColorFrame.Draw(textInString, ref font, textLoc, new Bgr(0, 0, 0));

        }

        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        byte[] convertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }
            }
            return depthFrame32;
        }
        /*
        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);
            currentDepthFrame = new Image<Gray, Byte>(320, 240);
            currentDepthFrame.Bytes = convertedDepthFrame;  // copy the byte of color frame into Egmu image
            depthImage.Source = ToBitmapSource(currentDepthFrame); 
            
        }
        */

        byte[] pixelData;
        void nui_showAugmentedColorFrame(object sender, SkeletonFrameReadyEventArgs e)
        {
            bool receivedData = false;

            //poll color frame and skeleton frame
            ColorImageFrame colorFrame = nui.ColorStream.OpenNextFrame(10);

            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            //check if we get colorFrame
            if (colorFrame != null){
             
                if(pixelData == null){
                    pixelData = new byte[colorFrame.PixelDataLength];
                
                }
                colorFrame.CopyPixelDataTo(pixelData);
                receivedData = true;
     
            }

            if(receivedData){
                //convert PlanarImage to Image for Egmu
                currentColorFrame = new Image<Bgr, Byte>(colorFrame.Width, colorFrame.Height);
                currentColorFrame.Bytes = pixelData;
                    //convertColorFrame(colorImage.Bits, currentColorFrame.Width, currentColorFrame.Height);  // copy the byte of color frame into Egmu image
            }
            //show colorFrame  
            overlayControlPanel();

            if (skeletons.Length != 0)
            {
                foreach (Skeleton data in skeletons)
                {
                    if (SkeletonTrackingState.Tracked == data.TrackingState)
                    {
                        // Get hands' position
                        List<int> depthList;
                        System.Windows.Media.PointCollection jointPositions = getJointPositionsAndDepths(out depthList, data.Joints, JointType.HandLeft, JointType.HandRight);

                        //drawJointPositions(jointPositions, depthList);
                        checkNaviMode(jointPositions, depthList); //handle left hand position

                        if (isNaviModeOn)
                        {
                            interpretCommand(jointPositions, depthList);
                        }
                    }
                }
            }
            
            //show test image
            TestImage.Source = ToBitmapSource(currentColorFrame);
        }

    }
}
