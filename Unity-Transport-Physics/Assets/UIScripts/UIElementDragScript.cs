using UnityEngine;
using System.Collections;

public class UIElementDragScript : MonoBehaviour {

    public bool allowDragging = true;
	public bool isConfinedToGameWindow = true;
	Vector3 canvasStartPosition;
	Vector3 canvasCurrentPosition;
	Vector3 canvasEndPosition;
	Vector3 mouseCurrentPosition;

	public void StartDragging()
	{
        canvasStartPosition = gameObject.GetComponent<RectTransform>().transform.position;
        canvasCurrentPosition = canvasStartPosition;
        mouseCurrentPosition = Input.mousePosition;
        Cursor.visible = false;  
	}
	
	public void DragCanvas()
	{	
		Vector3 positionChange = Input.mousePosition - mouseCurrentPosition;
		
		canvasCurrentPosition.y += (positionChange.y);
		canvasCurrentPosition.x += (positionChange.x);
		mouseCurrentPosition = Input.mousePosition;
				
		gameObject.GetComponent<RectTransform>().transform.position = canvasCurrentPosition;
	
		canvasEndPosition = canvasCurrentPosition;
	}
	
	public void EndDragging()
	{
		gameObject.GetComponent<RectTransform>().transform.position = canvasEndPosition;	
		Cursor.visible = true;
	}
}
