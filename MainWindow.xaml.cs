/////////////////////////////////////////////////////////////////////////
//
// Copyright © Microsoft Corporation.  All rights reserved.  
// This code is a “supplement” under the terms of the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU
// and is licensed under the terms of that license agreement. 
//
/////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Research.Kinect.Nui;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

[assembly:CLSCompliant(true)]
namespace Microsoft.Research.Kinect.Samples.CursorControl
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const float ClickThreshold = 0.33f;
		private const float SkeletonMaxX = 0.60f;
		private const float SkeletonMaxY = 0.40f;

		private Nui.Runtime _runtime = new Nui.Runtime();
		private NotifyIcon _notifyIcon = new NotifyIcon();


		public MainWindow()
		{
			InitializeComponent();

			// create tray icon
			_notifyIcon.Icon = new System.Drawing.Icon("CursorControl.ico");
			_notifyIcon.Visible = true;
			_notifyIcon.DoubleClick += delegate
			{
				this.Show();
				this.WindowState = WindowState.Normal;
				this.Focus();
			};
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// hook up our events for depth and sekeltal tracking
			_runtime.DepthFrameReady += _runtime_DepthFrameReady;
			_runtime.SkeletonFrameReady += _runtime_SkeletonFrameReady;

			try
			{
				// tell Kinect we need the depth buffer and skeletal tracking
				_runtime.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseSkeletalTracking);
			}
			catch(Exception ex)
			{
				MessageBox.Show("Could not initialize Kinect device: " + ex.Message);
			}

			// parameters used to smooth the skeleton data
			_runtime.SkeletonEngine.TransformSmooth = true;
			TransformSmoothParameters parameters = new TransformSmoothParameters();
			parameters.Smoothing = 0.7f;
			parameters.Correction = 0.3f;
			parameters.Prediction = 0.4f;
			parameters.JitterRadius = 1.0f;
			parameters.MaxDeviationRadius = 0.5f;
			_runtime.SkeletonEngine.SmoothParameters = parameters;

			try
			{
				// open the depth stream at the proper resolution
				_runtime.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
			}
			catch(Exception ex)
			{
				MessageBox.Show("Could not open depth stream: " + ex.Message);
			}
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			// shut down the Kinect device
			_notifyIcon.Visible = false;
			_runtime.Uninitialize();
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (WindowState == WindowState.Minimized)
				this.Hide();
		}

		void _runtime_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
		{
			foreach(SkeletonData sd in e.SkeletonFrame.Skeletons)
			{
				// the first found/tracked skeleton moves the mouse cursor
				if(sd.TrackingState == SkeletonTrackingState.Tracked)
				{
					// make sure both hands are tracked
					if(sd.Joints[JointID.HandLeft].TrackingState == JointTrackingState.Tracked &&
						sd.Joints[JointID.HandRight].TrackingState == JointTrackingState.Tracked)
					{
						int cursorX, cursorY;

						// get the left and right hand Joints
						Joint jointRight = sd.Joints[JointID.HandRight];
						Joint jointLeft = sd.Joints[JointID.HandLeft];

						// scale those Joints to the primary screen width and height
						Joint scaledRight = jointRight.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);
						Joint scaledLeft = jointLeft.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

						// figure out the cursor position based on left/right handedness
						if(LeftHand.IsChecked.GetValueOrDefault())
						{
							cursorX = (int)scaledLeft.Position.X;
							cursorY = (int)scaledLeft.Position.Y;
						}
						else
						{
							cursorX = (int)scaledRight.Position.X;
							cursorY = (int)scaledRight.Position.Y;
						}

						bool leftClick;

						// figure out whether the mouse button is down based on where the opposite hand is
						if((LeftHand.IsChecked.GetValueOrDefault() && jointRight.Position.Y > ClickThreshold) || 
								(!LeftHand.IsChecked.GetValueOrDefault() && jointLeft.Position.Y > ClickThreshold))
							leftClick = true;
						else
							leftClick = false;

						Status.Text = cursorX + ", " + cursorY + ", " + leftClick;
						NativeMethods.SendMouseInput(cursorX, cursorY, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, leftClick);

						return;
					}
				}
			}
		}

		void _runtime_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
		{
			// if the window is displayed, show the depth buffer
			if(this.WindowState == WindowState.Normal)
				video.Source = e.ImageFrame.ToBitmapSource();
		}
	}
}
