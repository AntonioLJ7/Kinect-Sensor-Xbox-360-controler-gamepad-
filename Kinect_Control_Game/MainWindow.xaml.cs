//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Kinect_Control_Juego
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Speech.AudioFormat;
    using Microsoft.Speech.Recognition;
    using System.Runtime.InteropServices;
  

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private const float RenderWidth = 640.0f;
        private const float RenderHeight = 480.0f;
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        private readonly Brush centerPointBrush = Brushes.Blue;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));        
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private readonly Pen trackedBonePen_b = new Pen(Brushes.Red, 6);

        //Valor de la coordenada 'Y' de la mano izquierda y cabeza
        private float miAlturaIzqda = 0;
        private float miAlturaCabeza = 0;
        private float miAlturaDer = 0;

        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        private DrawingGroup drawingGroup;
        private DrawingImage imageSource;

        [DllImport("user32")]
        public static extern int SetCursorPos(int x, int y);

        private const int MOUSEEVENT_MOVE = 0x0001;
        private const int MOUSEEVENT_LEFTDOWN = 0X0002;
        private const int MOUSEEVENT_LEFTUP = 0X0004;
        private const int MOUSEEVENT_RIGHTDOWN = 0X0008;

        [DllImport("user32.dll",CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]

        public static extern void mouse_event(int dwFlag, int dx, int dy, int cButtons, int dwExtraInfo);


        /// Activa el joystick virtual
        public Joystick joystick;

        /// Activa el sensor del kinect
        private KinectSensor sensor;

       
        /// Activa el reconociiento por voz
        private SpeechRecognitionEngine speechEngine;

        bool Juego = false;
        bool Mando = true;
        int test = 0;
        /// Initializes a new instance of the MainWindow class.
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }


        //***********************************************************************************************
        //********************************* RECONOCIMIENTO POR VOZ **************************************


        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (RecognizerInfo recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "es-ES".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        //***********************************************************************************************

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //Creamos el objeto joystick y lo inicializamos
            joystick = new Joystick();                  
            joystick.Inicializa();
            
            //creamos la imagen y la variable para dibujar esqueleto
            this.drawingGroup = new DrawingGroup();
            this.imageSource = new DrawingImage(this.drawingGroup);
            Image.Source = this.imageSource;


            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
              

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }

            }
            //Activamos la camara RGB y lo ocultamos
            sensor.ColorStream.Enable();
            sensor.ColorFrameReady += myKinect_ColorFrameReady;
            this.KinectVideo.Visibility = Visibility.Hidden;

            //Activamos la camara para esqueleto y lo ocultamosS
            this.sensor.SkeletonStream.Enable();
            this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
            this.Image.Visibility = Visibility.Hidden;

            this.sensor.ElevationAngle = 20;

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }



            //***********************************************************************************************
            //********************************* RECONOCIMIENTO POR VOZ **************************************

            RecognizerInfo ri = GetKinectRecognizer();


            if (null != ri)
            {

                this.speechEngine = new SpeechRecognitionEngine(ri.Id);


                var directions = new Choices();

                //Comando ACTIVAR/DESACTIVAR VIDEO
                directions.Add(new SemanticResultValue("video", "VIDEO"));
                directions.Add(new SemanticResultValue("no video", "NO VIDEO"));
                directions.Add(new SemanticResultValue("novideo", "NOVIDEO"));
                directions.Add(new SemanticResultValue("esqueleto", "ESQUELETO"));
                directions.Add(new SemanticResultValue("noesqueleto", "NOESQUELETO"));


                //Comando funcionar como mando 
                directions.Add(new SemanticResultValue("a", "A"));
                directions.Add(new SemanticResultValue("aa", "AA"));
                directions.Add(new SemanticResultValue("aceptar", "ACEPTAR"));
                directions.Add(new SemanticResultValue("equis", "EQUIS"));
                directions.Add(new SemanticResultValue("equix", "EQUIX"));
                directions.Add(new SemanticResultValue("y", "YY"));
                directions.Add(new SemanticResultValue("yy", "YY"));
                directions.Add(new SemanticResultValue("be", "BE"));
                directions.Add(new SemanticResultValue("atras", "ATRAS"));
                directions.Add(new SemanticResultValue("ERE", "ERE"));
                directions.Add(new SemanticResultValue("ELE", "ELE"));


                directions.Add(new SemanticResultValue("arriba", "ARRIBA"));
                directions.Add(new SemanticResultValue("abajo", "ABAJO"));
                directions.Add(new SemanticResultValue("derecha", "DERECHA"));
                directions.Add(new SemanticResultValue("izquierda", "IZQUIERDA"));

                directions.Add(new SemanticResultValue("pausa", "PAUSA"));
                directions.Add(new SemanticResultValue("star", "STAR"));
                directions.Add(new SemanticResultValue("modojuego", "MODOJUEGO"));
                directions.Add(new SemanticResultValue("modomando", "MODOMANDO"));


                var gb = new GrammarBuilder { Culture = ri.Culture };
                gb.Append(directions);

                var g = new Grammar(gb);

             
                speechEngine.LoadGrammar(g);
        
                speechEngine.SpeechRecognized += SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += SpeechRejected;


                speechEngine.SetInputToAudioStream(
                    sensor.AudioSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                this.statusBarText.Text = "No Speech Recognizer Ready!!";
            }
        }

        //***********************************************************************************************


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.AudioSource.Stop();

                this.sensor.Stop();
                this.sensor = null;
            }
            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }
        }


        //***********************************************************************************************
        //********************************* RECONOCIMIENTO POR VOZ **************************************

        /// <summary>
        /// Handler for recognized speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.3;
           

            if (e.Result.Confidence >= ConfidenceThreshold)
            {
                switch (e.Result.Semantics.Value.ToString())
                {   // Configuramos los comandos de voz
                    case "VIDEO":
                        this.KinectVideo.Visibility = Visibility.Visible;
                        break;
                    case "NOVIDEO":
                        this.KinectVideo.Visibility = Visibility.Hidden;
                        break;
                    case "ESQUELETO" :
                        this.Image.Visibility = Visibility.Visible;
                        break;
                    case "NOESQUELETO":
                        this.Image.Visibility = Visibility.Hidden;
                        break;
                    case "PAUSA":
                        joystick.PulsBoton(9);
                        break;
                    case "STAR":
                        joystick.PulsBoton(10);
                        break;
                    case "A":
                        joystick.PulsBoton(1);
                        break;
                    case "AA":
                        joystick.PulsBoton(1);
                        break;
                    case "ACEPTAR":
                        joystick.PulsBoton(1);
                        break;
                    case "BE":
                        joystick.PulsBoton(2);
                        break;
                    case "ATRAS":
                        joystick.PulsBoton(2);
                        break;
                    case "EQUIS":
                        joystick.PulsBoton(3);
                        break;
                    case "EQUIX":
                        joystick.PulsBoton(3);
                        break;
                    case "Y":
                        joystick.PulsBoton(4);
                        break;
                    case "YY":
                        joystick.PulsBoton(4);
                        break;
                    case "ERE":
                        joystick.PulsBoton(5);
                        break;
                    case "ELE":
                        joystick.PulsBoton(6);
                        break;
                    case "ARRIBA":
                        joystick.con_POV(0);
                        break;
                    case "ABAJO":
                        joystick.con_POV(2);
                        break;
                    case "IZQUIERDA":
                        joystick.con_POV(3);
                        break;
                    case "DERECHA":
                        joystick.con_POV(1);
                        break;
                    case "MODOJUEGO":
                        Mando = false;
                        Juego = true;
                        break;

                    case "MODOMANDO":
                        Mando = true;
                        Juego = false;
                        break;
                }
            }
        }

        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            //ClearRecognitionHighlights();
        }

        //***********************************************************************************************
        
        void myKinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null) return;
                byte[] colorData = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(colorData);

                    KinectVideo.Source = BitmapSource.Create(
                    colorFrame.Width, colorFrame.Height,
                    96, 97,
                    PixelFormats.Bgr32,
                    null,
                    colorData,
                    colorFrame.Width * colorFrame.BytesPerPixel
                    );
            }
        }


        //***********************************************************************************************
        //**************************** RECONOCIMIENTO DE ESQUELETO **************************************

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                double miJointThickness = JointThickness;
                if (joint.JointType == JointType.HandLeft)
                {
                    miAlturaIzqda = joint.Position.Y;

                   if (miAlturaIzqda > miAlturaCabeza/2)
                   {
                        if(Juego == true)
                        {
                            joystick.PulsBoton(1);
                        }
                   
                   } 

                }

                if (joint.JointType == JointType.HandRight)
                {

                    miAlturaDer = joint.Position.Y;

                    if (miAlturaDer > miAlturaCabeza/2)
                    {
                        if (Juego == true)
                        {
                            joystick.PulsBoton(3);
                        }
                    }

                }


                if (joint.JointType == JointType.ShoulderCenter)
                {
                    Point miPuntoPecho = this.SkeletonPointToScreen(joint.Position);

                    if (miPuntoPecho.X > RenderWidth / 2)
                    {
                        if(Juego == true)
                        {
                            joystick.ManPul_POV(1);
                        } 
                    }
                    else
                    {
                        if (Juego == true)
                        {
                            joystick.ManPul_POV(3);
                        }

                    }

                }

                else if (joint.JointType == JointType.Head)
                {
                    miAlturaCabeza = joint.Position.Y;

                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), miJointThickness, miJointThickness);
                }

            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                //drawPen = this.trackedBonePen;
                drawPen = this.trackedBonePen_b;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        //***********************************************************************************************


        //***********************************************************************************************
        //***************************** Control Elevación Kinect //**************************************

        //Eleva la camara de la kinect 3 grados
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                test = this.sensor.ElevationAngle + 3;
                if (test <= this.sensor.MaxElevationAngle)
                { 

                    this.sensor.ElevationAngle += 3;
                    test = 0;
                } 
            }
        }
        //Eleva la camara de la kinect -3 grados
        private void Button_Click_1(object sender, RoutedEventArgs e) 
        {
            if (null != this.sensor)
            {
                test = this.sensor.ElevationAngle - 3;
                if ( test >= this.sensor.MinElevationAngle )
                {
                    this.sensor.ElevationAngle -= 3;
                    test = 0;
                }

            }
        }

        //Eleva la camara de la kinect 0 grados
        private void Button_Click_2(object sender, RoutedEventArgs e) 
        {
            if (null != this.sensor)
            {
                this.sensor.ElevationAngle = 0;
            }
        }

       
    }
}