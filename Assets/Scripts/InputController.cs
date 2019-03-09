using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class InputController : MonoBehaviour {
	public AudioClip sliceMissSound;

	AudioSource audioSource;
	Vector3 sliceStartPos, sliceEndPos;
	bool isSlicing;
	GameObject startUI, endUI, lineUI;
	RectTransform lineRectTransform;
	float lineWidth;

    void Awake() {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Start () {	
		// Init GUI
		GameObject canvas = GameObject.Find("Canvas");
		GameObject pointPrefab = (GameObject)Resources.Load("Prefabs/Slice_Point");
		GameObject linePrefab = (GameObject)Resources.Load("Prefabs/Slice_Line");
		startUI = Instantiate(pointPrefab);
		endUI = Instantiate(pointPrefab);
		lineUI = Instantiate(linePrefab);
		startUI.transform.SetParent(canvas.transform);
		endUI.transform.SetParent(canvas.transform);
		lineUI.transform.SetParent(canvas.transform);
		startUI.SetActive(false);
		endUI.SetActive(false);
		lineUI.SetActive(false);
		lineRectTransform = lineUI.GetComponent<RectTransform>();
		lineWidth = lineRectTransform.rect.width;
	}
	
	void Update () {
		Vector3 mousePos;
		if (Input.GetMouseButtonDown(0)) {
            isSlicing = true;
            mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 1.0f);
            sliceStartPos = Camera.main.ScreenToWorldPoint(mousePos);
            startUI.SetActive(true);
            startUI.transform.position = Input.mousePosition;
		} else if(Input.GetMouseButtonUp(0) && isSlicing) {
			isSlicing = false;
			mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 1.0f); // "z" value defines distance from camera
			sliceEndPos = Camera.main.ScreenToWorldPoint(mousePos);
			if(sliceStartPos != sliceEndPos) {
				mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10.0f);
				Vector3 point3 = Camera.main.ScreenToWorldPoint(mousePos);
				Slice(new Plane(sliceStartPos, sliceEndPos, point3));
			}
		}
		if(isSlicing) {
			endUI.SetActive(true);
			endUI.transform.position = Input.mousePosition;
            Vector2 sliceVect = endUI.transform.position - startUI.transform.position;
            lineUI.SetActive(true);
            lineUI.transform.position = (endUI.transform.position + startUI.transform.position) / 2f;
            lineRectTransform.sizeDelta = new Vector2(lineWidth, sliceVect.magnitude);
            lineUI.transform.rotation = Quaternion.FromToRotation(Vector3.up, sliceVect.normalized);
		} else {
			startUI.SetActive(false);
			endUI.SetActive(false);
			lineUI.SetActive(false);
		}
	}
	
	// Detect & slice "sliceable" GameObjects whose bounding box intersects slicing plane
	private void Slice(Plane plane) {
		SliceableObject[] sliceableTargets = (SliceableObject[])FindObjectsOfType( typeof(SliceableObject) );
		foreach(SliceableObject sliceableTarget in sliceableTargets) {
			GameObject target = sliceableTarget.gameObject;
            MeshSlicer.SliceMesh(target, plane);
        }
	}
}
