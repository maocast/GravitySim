using UnityEngine;
using System.Collections;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class CaptureScript : MonoBehaviour
{
	//Debug constant
	const bool DEBUG_MODE = false;
	
	//Image height and width
	const float CAPTURE_WIDTH = 300f;
	const float CAPTURE_HEIGHT = 300f;
	
	//Dimensions of the simulation
	const float HEIGHT_FACTOR = 1f / 30f; //10m tall
	const float WIDTH_FACTOR = 1f / 30f; //10m wide
	
	//Physics simulation constants
	const float maxMass = 100f; //100 kg
	const float maxFriction = 1.0f; // Max coefficient for friction
	const float maxBounciness = 1.0f; //Coefficient for inelastic collisions.
	
	//Arrays containing CVpoints (unscaled to unity3d environment)
	private List<CvBox2D> boxes;
	private List<CvBox2D> lines;
	private List<CvCircleSegment> circles;
	
	//List holding all the game objects that were
	//created for this simulation "run" 
	private List<GameObject> createdObjects;
	
	//Plane texture -- Used to project original image, when necessary.
	public Texture2D texture;

	//Path to the image to be simulated
	private string img_name;

	// Use this for initialization
	public void DoStart ()
	{
		using(IplImage imgSrc = Cv.LoadImage(img_name,LoadMode.Color))
		using (IplImage imgHSV = new IplImage(imgSrc.Size, BitDepth.U8, 3))
		{
			if(DEBUG_MODE)
				Debug.Log ("width:" + imgSrc.Width + " height:" + imgSrc.Height + " Color Type:" + imgSrc.Depth);
		
			//Initialize lists for 2D detected structures
			createdObjects = new List<GameObject>();
			boxes = new List<CvBox2D>();
			lines = new List<CvBox2D>();
			circles = new List<CvCircleSegment>();
			
			//Detect cirlces and squares and lines
			findCircles (imgSrc);
			findBoxesAndLines (imgSrc);
			
			//Convert to 3d Models
			//Must convert image to hsv colorspace
			Cv.CvtColor(imgSrc, imgHSV,ColorConversion.BgrToHsv);
			
			//Instantiate different objects in simulation environment
			instantiateSpheres(imgHSV, imgSrc);
			instantiateLines(imgHSV, imgSrc);
			instantiateCubes(imgHSV, imgSrc);
			
			//setImageToTexture(imgSrc);
		}
	}
	
	//Sets the path, for the next simulation
	public void setFile(string filePath)
	{
		Resources.Load(filePath);
		img_name = filePath;
	}
	
	//Destroy all the objects that were created in the simulation
	public void destroyCreatedObjects()
	{
		if(createdObjects != null)
			for(int i = 0; i < createdObjects.Count; i++)
			{
				GameObject go = createdObjects[i];
				Destroy(go);
			}
	}
	
	#region instantiate
	/**
	 * Converts the colors to physical properties for the simulation.
	 * Also creates the rigidbodies and the materials, for friction and bounciness
	 */
	void attachPhysicalProperties(GameObject go, CvColor colorHSV, CvColor colorBGR)
	{
		createdObjects.Add(go);
		
		float H = colorHSV.B*2;
		float S = 100f*colorHSV.G/255;
		float V = 100f*colorHSV.R/255;
		
		//Initialize physical properties
		PhysicMaterial pm = new PhysicMaterial();
		go.collider.material = pm;
		
		//Set color to original color of image.
		go.renderer.material.color = new Color((float)colorBGR.R/255, (float)colorBGR.G/255, (float)colorBGR.B/255);
		
		//Black
		if(V < 15)
		{
			pm.staticFriction = 0.1f;
			pm.dynamicFriction = 0.1f;
		}
		//Green
		else if(60 <= H && H < 180)
		{
			Rigidbody rigidbody = go.AddComponent<Rigidbody>();
			//No motion along Z axis
			rigidbody.constraints = RigidbodyConstraints.FreezePositionZ;
		}
		//Blue
		else if(180 <= 180 && H < 300)
		{
			Rigidbody rigidbody = go.AddComponent<Rigidbody>();
			//No motion along z axis
			rigidbody.constraints = RigidbodyConstraints.FreezePositionZ;
			
			rigidbody.mass = (float)S*maxMass/100f;
			
			pm.bounciness = (float)(V/100f)*maxFriction;
			
			pm.dynamicFriction =(float)(1.0f - V/100f)*maxFriction;
			pm.staticFriction = (float)(1.0f - V/100f)*maxFriction;
			
		}
		//Red
		else
		{
			Rigidbody rigidbody = go.AddComponent<Rigidbody>();
			//No motion along Z axis
			rigidbody.constraints = RigidbodyConstraints.FreezePositionZ;
			
			rigidbody.mass = (float)S*maxMass/100f;
			
			pm.dynamicFriction =(float)(1.0f - V/100)*maxFriction;
			pm.staticFriction = (float)(1.0f - V/100)*maxFriction;
		}
	}
	
	void instantiateSpheres (IplImage imgHSV, IplImage imgBGR)
	{
		for(int i = 0; i < circles.Count; i++)
		{
			CvCircleSegment ccs = circles[i];
			GameObject sp = GameObject.CreatePrimitive (PrimitiveType.Sphere);
			
			//Locate sphere in space and scale
			sp.transform.position = new Vector3 (ccs.Center.X * WIDTH_FACTOR, (CAPTURE_HEIGHT - ccs.Center.Y) * HEIGHT_FACTOR, 0);
			sp.transform.localScale = new Vector3 (ccs.Radius * WIDTH_FACTOR * 2, ccs.Radius * HEIGHT_FACTOR * 2, ccs.Radius * WIDTH_FACTOR * 2);
			
			//Find colors
			CvColor colorHSV = imgHSV[(int)ccs.Center.Y, (int)ccs.Center.X];
			CvColor colorBGR = imgBGR[(int)ccs.Center.Y, (int)ccs.Center.X];

			//Attach physical properties
			attachPhysicalProperties(sp, colorHSV, colorBGR);
		}
	}
	
	void instantiateCubes (IplImage imgHSV, IplImage imgBGR)
	{	
		for(int i = 0; i < boxes.Count; i++)
		{
			GameObject sp = GameObject.CreatePrimitive (PrimitiveType.Cube);
			CvBox2D box = boxes[i];
			
			//Scale to unity coordinates
			sp.transform.position = new Vector3 (box.Center.X * WIDTH_FACTOR, (CAPTURE_HEIGHT - box.Center.Y) * HEIGHT_FACTOR, 0);
			sp.transform.localScale = new Vector3 (box.Size.Width * WIDTH_FACTOR, box.Size.Height * HEIGHT_FACTOR, 40 * WIDTH_FACTOR);
			
			//Find colors
			CvColor colorHSV = imgHSV[(int)box.Center.Y, (int)box.Center.X];
			CvColor colorBGR = imgBGR[(int)box.Center.Y, (int)box.Center.X];
			
			//Attach Physical properties
			attachPhysicalProperties(sp,colorHSV,colorBGR);
		}
	}
	
	void instantiateLines(IplImage imgHSV, IplImage imgBGR)
	{
		for(int i = 0; i < lines.Count; i++)
		{
			CvBox2D line = lines[i];
			GameObject sp = GameObject.CreatePrimitive (PrimitiveType.Cube);
			
			//Find Colors
			CvColor colorHSV = imgHSV[(int)line.Center.Y, (int)line.Center.X];
			CvColor colorBGR = imgBGR[(int)line.Center.Y, (int)line.Center.X];
			
			//Scale to unity coordinates and adjust for "thin" lines
			sp.transform.position = new Vector3 (line.Center.X * WIDTH_FACTOR, (CAPTURE_HEIGHT - line.Center.Y) * HEIGHT_FACTOR, 0);
			//With box2d height and width are inverted?!?!
			//Adjust for 0 width or height
			if(line.Size.Height == 0)
				sp.transform.localScale = new Vector3 (5 * WIDTH_FACTOR, line.Size.Width * HEIGHT_FACTOR, 50 * WIDTH_FACTOR);
			else
				sp.transform.localScale = new Vector3 (line.Size.Height * WIDTH_FACTOR, 5 * HEIGHT_FACTOR, 50 * WIDTH_FACTOR);
			sp.transform.Rotate(new Vector3(0,0,1),line.Angle);
			
			//Attach physical properties
			attachPhysicalProperties(sp,colorHSV, colorBGR);
			
		}
	}
	#endregion
	
	#region imageToTexture
	//Method used to draw B&W image on plane
	void setBWImageToTexture (IplImage img)
	{
		//Image must be fliped to color according to unity's texture format
		img.Flip(img,FlipMode.Y);
		
		texture = new Texture2D (img.Width, img.Height, TextureFormat.RGBA32, false);
		
		Color[] cols = new Color[texture.width * texture.height];
        
		for (int y = 0; y < texture.height; y++) {
			for (int x = 0; x < texture.width; x++) {
				CvColor col = img.Get2D (y, x);
				cols [y * texture.width + x] = new Color ((col.B / 255.0f), (col.B / 255.0f), (col.B / 255.0f), 1.0f);
			}
		}
		texture.SetPixels (cols);
		texture.Apply ();
	}

	//Method used to draw BGR image as a texture on plane
	void setImageToTexture (IplImage img)
	{
		//Image must be fliped to color according to unity's texture format
		img.Flip(img,FlipMode.Y);
		
		texture = new Texture2D (img.Width, img.Height, TextureFormat.RGBA32, false);
		
		Color[] cols = new Color[texture.width * texture.height];
        
		for (int y = 0; y < texture.height; y++) {
			for (int x = 0; x < texture.width; x++) {
				CvColor col = img.Get2D (y, x);
					
				//Will set image as appears in file
				cols [y * texture.width + x] = new Color (col.R / 255.0f, col.G / 255.0f, col.B / 255.0f, 1.0f);
				
				//Will color image to view way that unity assigns textures
				//float fact = ((float)y*(float)x)/((float)texture.height * (float)texture.width);
				//cols [y * texture.width + x] = new Color (fact*col.R / 255.0f, fact*col.G / 255.0f, fact*col.B / 255.0f, 1.0f);
			}
		}
		texture.SetPixels (cols);
		texture.Apply ();
	}
	#endregion
	
	#region circles
	void findCircles (IplImage imgSrc)
	{	
		using (IplImage imgGray = new IplImage(imgSrc.Size, BitDepth.U8, 1))
		using (IplImage imgHough = imgSrc.Clone()) {
			using (CvMemStorage storage = new CvMemStorage()) {
				storage.Clear ();
				
				//Prepare image for hough processing
				//Pre-Processing
				Cv.CvtColor (imgSrc, imgGray, ColorConversion.BgrToGray);
				Cv.Smooth (imgGray, imgGray, SmoothType.Gaussian, 9);
				//Cv.Canny(imgGray, imgGray, 75, 150, ApertureSize.Size3);
				
				//Do HoughCircles
				//CvSeq<CvCircleSegment> seq = imgGray.HoughCircles (storage, HoughCirclesMethod.Gradient, 3, imgGray.Height / 4, 200, 100, 10, 100);
				//CvSeq<CvCircleSegment> seq = imgGray.HoughCircles (storage, HoughCirclesMethod.Gradient, 1, imgGray.Height / 4, 150, 55, 0, 0);
				//CvSeq<CvCircleSegment> seq = imgGray.HoughCircles (storage, HoughCirclesMethod.Gradient, 1, imgGray.Height / 4, 200, 50, 10, 100);
				CvSeq<CvCircleSegment> seq = imgGray.HoughCircles (storage, HoughCirclesMethod.Gradient, 2, imgGray.Height / 3, 200, 55, 10, 100);
				
				//Draw circles on image
				if(DEBUG_MODE)
					Debug.Log ("Find Circles -- Number of Circles: " + seq.Total);
				
				for (int i = 0; i < seq.Total; i++) {
					CvCircleSegment item = (CvCircleSegment)seq [i];
					
					if(DEBUG_MODE){
						imgHough.Circle (item.Center, (int)item.Radius, CvColor.Red, 3);
						Debug.Log ("Find Circles -- " + "Circle: " + (i+1) + "Center: " + item.Center + " Radius: " + (int)item.Radius);
					}
				
					//Add circles that dont have a white center (not real circles!!)
					//This is sort of a hack, additional level of protection...
					CvColor color = imgSrc[(int)item.Center.Y, (int)item.Center.X];
					if(!(color.R > 230 && color.G > 230 && color.B > 230))
						circles.Add(item);
				}
				
				storage.Clear ();
				
				//Draw image on plane
				if (DEBUG_MODE){
					//setBWImageToTexture (imgGray);
					setImageToTexture (imgHough);
				}
			}
		}
	}
	#endregion
	
	#region squares
	/// <summary>
	/// helper function:
	/// finds a cosine of Angle between vectors
	/// from pt0->pt1 and from pt0->pt2 
	/// </summary>
	/// <param name="pt1"></param>
	/// <param name="pt2"></param>
	/// <param name="pt0"></param>
	/// <returns></returns>
	static double Angle (CvPoint pt1, CvPoint pt2, CvPoint pt0)
	{
		float dx1 = pt1.X - pt0.X;
		float dy1 = pt1.Y - pt0.Y;
		float dx2 = pt2.X - pt0.X;
		float dy2 = pt2.Y - pt0.Y;
		return (dx1 * dx2 + dy1 * dy2) / Mathf.Sqrt ((dx1 * dx1 + dy1 * dy1) * (dx2 * dx2 + dy2 * dy2) + 1e-10f);
	}

	/// <summary>
	/// Adds all detected "boxes" to the list of boxes
	/// </summary>
	/// <param name="imgSrc"></param>
	public void findBoxesAndLines (IplImage imgSrc)
	{
		using (IplImage img = imgSrc.Clone())
		using(CvMemStorage storage = new CvMemStorage()){
			CvSize sz = new CvSize (img.Width & -2, img.Height & -2);
			IplImage timg = img.Clone (); // make a copy of input image
			IplImage gray = new IplImage (sz, BitDepth.U8, 1);
			IplImage pyr = new IplImage (sz.Width / 2, sz.Height / 2, BitDepth.U8, 3);
	
			// select the maximum ROI in the image
			// with the width and height divisible by 2
			//timg.ROI = new CvRect (0, 0, sz.Width, sz.Height);
	
			// down-Scale and upscale the image to filter out the noise
			Cv.PyrDown (timg, pyr, CvFilter.Gaussian5x5);
			Cv.PyrUp (pyr, timg, CvFilter.Gaussian5x5);
			IplImage tgray = new IplImage (sz, BitDepth.U8, 1);
	
			Cv.CvtColor (timg, tgray, ColorConversion.BgrToGray);
			Cv.Smooth (tgray, tgray, SmoothType.Gaussian, 9);
	
			Cv.Canny (tgray, gray, 0, 100, ApertureSize.Size5);
			// dilate canny output to remove potential
			// holes between edge segments 
			Cv.Dilate (gray, gray, null, 1);
	
			// find contours and store them all as a list
			CvSeq<CvPoint> contours;
			Cv.FindContours (gray, storage, out contours, CvContour.SizeOf, ContourRetrieval.List, ContourChain.ApproxSimple, new CvPoint (0, 0));
			
			// test each contour
			while (contours != null) {
				// approximate contour with accuracy proportional
				// to the contour perimeter
				CvSeq<CvPoint> result = Cv.ApproxPoly (contours, CvContour.SizeOf, storage, ApproxPolyMethod.DP, contours.ContourPerimeter () * 0.02, false);
				
				// square contours should have 4 vertices after approximation
				// relatively large area (to filter out noisy contours)
				// and be convex.
				// Note: absolute value of an area is used because
				// area may be positive or negative - in accordance with the
				// contour orientation
				
				//Only considering contours with certain orientation, i.e. no Mathf.Abs(...).
				//if (result.Total == 4 && Mathf.Abs ((float)result.ContourArea (CvSlice.WholeSeq)) > 500 && result.CheckContourConvexity ()) {
				if (result.Total == 4 && (float)result.ContourArea (CvSlice.WholeSeq) < 500 && result.CheckContourConvexity ()){
					double s = 0;
	
					for (int i = 0; i < 5; i++) {
						// find minimum Angle between joint
						// edges (maximum of cosine)
						if (i >= 2) {
							double t = Mathf.Abs ((float)Angle (result [i%4].Value, result [i - 2].Value, result [i - 1].Value));
							s = s > t ? s : t;
						}
					}
					
					// if cosines of all angles are small
					// (all angles are ~90 degree) then write quandrange
					// vertices to resultant sequence 
					if (s < 0.3) {
						
						if(DEBUG_MODE){
							Debug.Log ("Area: " + result.ContourArea());
						}
						
						float centerX = (result[0].Value.X + result[3].Value.X)/2f;
						float centerY = (result[0].Value.Y + result[1].Value.Y)/2f;
						float width = Mathf.Abs(result[3].Value.X-result[0].Value.X);
						float height = Mathf.Abs(result[1].Value.Y - result[0].Value.Y);
						
						CvBox2D box = new CvBox2D(new CvPoint2D32f(centerX,centerY), new CvSize2D32f(width,height),0);
						boxes.Add(box);
	
						//Draws the square
						if(DEBUG_MODE)
						{
							CvPoint[] pt = new CvPoint[4];

							// read 4 vertices
							pt [0] = result [0].Value;
							pt [1] = result [1].Value;
							pt [2] = result [2].Value;
							pt [3] = result [3].Value;
			
							// draw the square as a closed polyline 
							Cv.PolyLine (img, new CvPoint[][] { pt }, true, CvColor.Blue, 1, LineType.AntiAlias, 0);
						}
					}
				}
				//Check for lines.
				//if(result.Total <= 6 && Mathf.Abs ((float)result.ContourArea (CvSlice.WholeSeq)) > 100 && result.CheckContourConvexity() && !isRectangle){
				else if(result.Total == 2){
					CvBox2D rect = result.MinAreaRect2();
					lines.Add(rect);		
				}
	
				// take the next contour
				contours = contours.HNext;
			}
	
			// release all the temporary images
			gray.Dispose ();
			pyr.Dispose ();
			tgray.Dispose ();
			timg.Dispose ();
			
			if (DEBUG_MODE)
				setImageToTexture(img);
		}
	}
	#endregion
}
