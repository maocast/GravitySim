using UnityEngine;
using System.Collections;

public class GUIMenu : MonoBehaviour
{
	private bool fileBrowser;
	private string location;
	
	private Vector2 directoryScroll = Vector2.zero;
	private Vector2 fileScroll = Vector2.zero;
	
	private CaptureScript cs;
	
	void OnGUI ()
	{
		// Make the first button. If it is pressed, Application.Loadlevel (1) will be executed
		if (GUI.Button (new Rect (10, 10, 80, 20), "Load File")) {
			fileBrowser = true;			
		}
		
		if (fileBrowser) {
			GUI.Window (0, new Rect ((Screen.width - 430) / 2, (Screen.height - 380) / 2, 430, 380), FileBrowserWindow, "Browse");
			return;
		}
	}
	
	public void Start()
	{
		fileBrowser = false;
		location = "C:";
	}
	
	public void FileBrowserWindow (int idx)
	{
		if (FileBrowserComponent.FileBrowser (ref location, ref directoryScroll, ref fileScroll)) {
			fileBrowser = false;
			
			location.Replace('\\','/');
			
			GameObject go = GameObject.Find("Capture");
			CaptureScript cs = go.GetComponent<CaptureScript>();
			
			cs.destroyCreatedObjects();
			cs.setFile(location);
			cs.DoStart();
		}
	}
}