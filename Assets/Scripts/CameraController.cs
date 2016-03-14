using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour {
    Vector3 lastMousePos;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        if (lastMousePos == null) lastMousePos = Input.mousePosition;
        Vector3 mouseDeltaPos = Input.mousePosition - lastMousePos;
        if (Input.GetMouseButton(0)) {
            //transform.RotateAround()
        }
        lastMousePos = Input.mousePosition;
	}
}
