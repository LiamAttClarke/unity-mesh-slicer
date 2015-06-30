using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Slicer : MonoBehaviour {
	public GameObject obj, slice;
	private Transform objTransform;
	private Mesh objMesh, slice1Mesh, slice2Mesh;
	private Vector3 sliceStartPos, sliceEndPos;
	private bool isSlicing = false;
	struct Plane {
		public readonly Vector3 point;
		public readonly Vector3 normal;
		public readonly float d;
		// Plane given point and normal direction
		public Plane(Vector3 planeP, Vector3 planeN) {
			this.point = planeP;
			this.normal = planeN;
			this.d = 0;
			this.d = PlaneDValue(planeP, planeN);
		}
		// Plane given 3 points
		public Plane(Vector3 point1, Vector3 point2, Vector3 point3) {
			this.point = point1;
			this.normal = Vector3.zero;
			this.d = 0;
			this.normal = PlaneNormal(point1, point2, point3);
			this.d = PlaneDValue(point1, this.normal);
		}
		// Calculate "D" value for the eqation of a plane "Ax + By + Cz = D"
		private float PlaneDValue(Vector3 planeP, Vector3 planeN) {
			float d = (planeN.x * planeP.x) + (planeN.y * planeP.y) + (planeN.z * planeP.z);
			return d;
		}
		// Calculate plane's normal given 3 points on the plane
		private Vector3 PlaneNormal(Vector3 point1, Vector3 point2, Vector3 point3) {
			Vector3 dir1 = (point2 - point1).normalized;
			Vector3 dir2 = (point3 - point1).normalized;
			return Vector3.Cross(dir2, dir1);
		}
	}
	
	// Initialization
	void Start () {
		objMesh = obj.GetComponent<MeshFilter>().mesh;
		objTransform = obj.transform;
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 mousePos;
		// Define Slicing Plane
		if (Input.GetMouseButtonDown (0)) {
			mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 1.0f);
			sliceStartPos = Camera.main.ScreenToWorldPoint(mousePos);
			isSlicing = true;
		} else if(Input.GetMouseButtonUp (0) && isSlicing) {
			mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 1.0f); // "z" value defines distance from camera
			sliceEndPos = Camera.main.ScreenToWorldPoint(mousePos);
			if(sliceStartPos != sliceEndPos) {
				mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 2.0f);
				Vector3 point3 = Camera.main.ScreenToWorldPoint(mousePos);
				Plane plane = new Plane(sliceStartPos, sliceEndPos, point3);
				SliceMesh(plane, objMesh);
			}
			isSlicing = false;
		}
	}
	
	// Slice mesh along plane intersection
	void SliceMesh(Plane plane, Mesh mesh) {
		// original
		Vector3[] meshVerts = mesh.vertices;
		int[] meshTris = mesh.triangles;
		// sliced
		List<Vector3> slice1Verts = new List<Vector3>();
		List<Vector3> slice2Verts = new List<Vector3>();
		List<int> slice1Tris = new List<int>();
		List<int> slice2Tris = new List<int>();
		int slice1TriCounter = 1;
		int slice2TriCounter = 1;
		// Loop through triangles
		int numOfTris = meshTris.Length / 3;
		for(int i = 1; i <= numOfTris; i++) {
			// local space vertices
			Vector3 localVert1 = meshVerts[meshTris[i * 3 - 3]];
			Vector3 localVert2 = meshVerts[meshTris[i * 3 - 2]];
			Vector3 localVert3 = meshVerts[meshTris[i * 3 - 1]];
			// world space vertices
			Vector3 worldVert1 = objTransform.TransformPoint(localVert1);
			Vector3 worldVert2 = objTransform.TransformPoint(localVert2);
			Vector3 worldVert3 = objTransform.TransformPoint(localVert3);
			// Side test: (0) = intersecting plane; (+) = above plane; (-) = below plane;
			float prod1 = Vector3.Dot(plane.normal, worldVert1 - plane.point);
			float prod2 = Vector3.Dot(plane.normal, worldVert2 - plane.point);
			float prod3 = Vector3.Dot(plane.normal, worldVert3 - plane.point);
			// assign triangles that do not intersect plane
			if(prod1 > 0 && prod2 > 0 && prod3 > 0) { // Slice 1
				slice1Verts.Add(localVert1);
				slice1Verts.Add(localVert2);
				slice1Verts.Add(localVert3);
				slice1Tris.Add(slice1TriCounter * 3 - 3);
				slice1Tris.Add(slice1TriCounter * 3 - 2);
				slice1Tris.Add(slice1TriCounter * 3 - 1);
				slice1TriCounter++;
			} else if(prod1 < 0 && prod2 < 0 && prod3 < 0) { // Slice 2
				slice2Verts.Add(localVert1);
				slice2Verts.Add(localVert2);
				slice2Verts.Add(localVert3);
				slice2Tris.Add(slice2TriCounter * 3 - 3);
				slice2Tris.Add(slice2TriCounter * 3 - 2);
				slice2Tris.Add(slice2TriCounter * 3 - 1);
				slice2TriCounter++;
			} else {
				// Determine which line segment intersects plane
				Vector3 commonPoint, p1, p2;
				if(prod1 * prod2 > 0) {
					commonPoint = worldVert3;
					p1 = worldVert1;
					p2 = worldVert2;
				} else if(prod1 * prod3 > 0) {
					commonPoint = worldVert2;
					p1 = worldVert1;
					p2 = worldVert3;
				} else {
					commonPoint = worldVert1;
					p1 = worldVert2;
					p2 = worldVert3;
				}
				// POIs
				Vector3 poi1 = objTransform.InverseTransformPoint(VectorPlanePOI(commonPoint, commonPoint - p1, plane));
				Vector3 poi2 = objTransform.InverseTransformPoint(VectorPlanePOI(commonPoint, commonPoint - p2, plane));
				Vector3 p = objTransform.InverseTransformPoint(commonPoint);
				//
				if(Vector3.Dot(plane.normal, commonPoint - plane.point) > 0) {
					slice1Verts.Add(poi1);
					slice1Verts.Add(poi2);
					slice1Verts.Add(p);
					slice1Tris.Add(slice1TriCounter * 3 - 3);
					slice1Tris.Add(slice1TriCounter * 3 - 2);
					slice1Tris.Add(slice1TriCounter * 3 - 1);
					slice1TriCounter++;
				} else {
					slice2Verts.Add(poi1);
					slice2Verts.Add(poi2);
					slice2Verts.Add(p);
					slice2Tris.Add(slice2TriCounter * 3 - 3);
					slice2Tris.Add(slice2TriCounter * 3 - 2);
					slice2Tris.Add(slice2TriCounter * 3 - 1);
					slice2TriCounter++;
				}
			}
		}
		// Build Meshes
		slice1Mesh = new Mesh();
		slice2Mesh = new Mesh();
		slice1Mesh.vertices = slice1Verts.ToArray();
		slice2Mesh.vertices = slice2Verts.ToArray();
		slice1Mesh.triangles = slice1Tris.ToArray();
		slice2Mesh.triangles = slice2Tris.ToArray();
		//slice1Mesh.RecalculateNormals();
		//slice2Mesh.RecalculateNormals();
		GameObject slice1 = (GameObject)Instantiate(slice, objTransform.position, objTransform.rotation);
		GameObject slice2 = (GameObject)Instantiate(slice, objTransform.position, objTransform.rotation);
		slice1.transform.localScale = objTransform.localScale;
		slice1.GetComponent<MeshFilter>().mesh = slice1Mesh;
		slice2.GetComponent<MeshFilter>().mesh = slice2Mesh;
		Destroy(obj);
		
	}
	
	// Point of intersection between vector and a plane
	Vector3 VectorPlanePOI(Vector3 point, Vector3 direction, Plane plane) {
		// Plane: Ax + By + Cz = D
		// Vector: r = (p.x, p.y, p.z) + t(d.x, d.y, d.z)
		float A = plane.normal.x;
		float B = plane.normal.y;
		float C = plane.normal.z;
		float D = plane.d;
		float t = (-A*point.x - B*point.y - C*point.z - D) / (A*direction.x + B*direction.y + C*direction.z);
		float x = point.x + t*direction.x;		
		float y = point.y + t*direction.y;		
		float z = point.z + t*direction.z;
		return new Vector3(x, y, z);
	}
}
