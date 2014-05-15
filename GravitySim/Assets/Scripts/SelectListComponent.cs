using UnityEngine;
using System.Collections;

public static class SelectListComponent
{

	public static object SelectList (ICollection list, object selected, GUIStyle defaultStyle, GUIStyle selectedStyle)
	{			
		foreach (object item in list) {
			if (GUILayout.Button (item.ToString (), (selected == item) ? selectedStyle : defaultStyle)) {
				if (selected == item) {
					// Clicked an already selected item. Deselect.
					selected = null;
				} else {
					selected = item;
				}
			}
		}
 
		return selected;
	}
 
 
 
	public delegate bool OnListItemGUI (object item,bool selected,ICollection list);
 
	public static object SelectList (ICollection list, object selected, OnListItemGUI itemHandler)
	{
		ArrayList itemList;
 
		itemList = new ArrayList (list);
 
		foreach (object item in itemList) {
			if (itemHandler (item, item == selected, list)) {
				selected = item;
			} else if (selected == item) {
				// If we *were* selected, but aren't any more then deselect
				selected = null;
			}
		}
 
		return selected;
	}
}
