using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Slicer : MonoBehaviour {
	public GameObject obj, slice;
	public GameObject target;
	private Transform objTransform;
	private Mesh objMesh, slice1Mesh, slice2Mesh;
	private Vector3 sliceStartPos, sliceEndPos;
	private bool isSlicing = false;
	struct Plane {
		public readonly Vector3 point;
		public readonly Vector3 normal;
		public readonly float d;
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
			float d = -1.0f * ((planeN.x * planeP.x) + (planeN.y * planeP.y) + (planeN.z * planeP.z));
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
				mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10.0f);
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
			Vector3[] localVerts = {
				meshVerts[meshTris[i * 3 - 3]],
				meshVerts[meshTris[i * 3 - 2]],
				meshVerts[meshTris[i * 3 - 1]]
			};
			// world space vertices
			Vector3[] worldVerts = {
				objTransform.TransformPoint(localVerts[0]),
				objTransform.TransformPoint(localVerts[1]),
				objTransform.TransformPoint(localVerts[2])
			};
			// Side test: (0) = intersecting plane; (+) = above plane; (-) = below plane;
			float prod1 = Vector3.Dot(plane.normal, worldVerts[0] - plane.point);
			float prod2 = Vector3.Dot(plane.normal, worldVerts[1] - plane.point);
			float prod3 = Vector3.Dot(plane.normal, worldVerts[2] - plane.point);
			// assign triangles that do not intersect plane
			if(prod1 > 0 && prod2 > 0 && prod3 > 0) { // Slice 1
				for(int j = 0; j < localVerts.Length; j++) {
					slice1Verts.Add(localVerts[j]);
					slice1Tris.Add(slice1TriCounter * 3 - (3 - j));
				}
				slice1TriCounter++;
			} else if(prod1 < 0 && prod2 < 0 && prod3 < 0) { // Slice 2
				for(int j = 0; j < localVerts.Length; j++) {
					slice2Verts.Add(localVerts[j]);
					slice2Tris.Add(slice2TriCounter * 3 - (3 - j));
				}
				slice2TriCounter++;
			} else {
				// Determine which line segment intersects plane
				Vector3 p1, p2, p3;
				int[] triOrder; // triangle vertex-order CW vs. CCW
				if(prod1 * prod2 > 0) {
					p1 = worldVerts[2];
					p2 = worldVerts[0];
					p3 = worldVerts[1];
					triOrder = new int[] {4,0,3, 2,4,1, 1,4,3};
				} else if(prod1 * prod3 > 0) {
					p1 = worldVerts[1];
					p2 = worldVerts[0];
					p3 = worldVerts[2];
					triOrder = new int[] {3,0,4, 2,1,4, 4,1,3};
				} else {
					p1 = worldVerts[0];
					p2 = worldVerts[1];
					p3 = worldVerts[2];
					triOrder = new int[] {0,3,4, 4,1,2, 4,3,1};
				}
				// bisected triangle vertices - local space
				Vector3[] localTriVerts = {
					objTransform.InverseTransformPoint(p1),
					objTransform.InverseTransformPoint(p2),
					objTransform.InverseTransformPoint(p3),
					objTransform.InverseTransformPoint(VectorPlanePOI(p1, (p2 - p1).normalized, plane)),
					objTransform.InverseTransformPoint(VectorPlanePOI(p1, (p3 - p1).normalized, plane))
				};
				// Add bisected triangle to slice respectively
				if(Vector3.Dot(plane.normal, p1 - plane.point) > 0) {
					// Slice 1
					for(int j = 0; j < 3; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j));
					}
					slice1TriCounter++;
					// Slice 2
					for(int j = 3; j < 6; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j % 3));
					}
					slice2TriCounter++;
					for(int j = 6; j < 9; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j % 3));
					}
					slice2TriCounter++;
				} else {
					// Slice 2
					for(int j = 0; j < 3; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j));
					}
					slice2TriCounter++;
					// Slice 1
					for(int j = 3; j < 6; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j % 3));
					}
					slice1TriCounter++;
					for(int j = 6; j < 9; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j % 3));
					}
					slice1TriCounter++;
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
		slice2.transform.localScale = objTransform.localScale;
		slice1.GetComponent<MeshFilter>().mesh = slice1Mesh;
		slice2.GetComponent<MeshFilter>().mesh = slice2Mesh;
		// Delete original
		Destroy(obj);
	}
	
	// Point of intersection between vector and a plane
	Vector3 VectorPlanePOI(Vector3 point, Vector3 direction, Plane plane) {
		// Plane: Ax + By + Cz = D
		// Vector: r = (p.x, p.y, p.z) + t(d.x, d.y, d.z)
		float a = plane.normal.x;
		float b = plane.normal.y;
		float c = plane.normal.z;
		float d = plane.d;
		float t = -1 * (a*point.x + b*point.y + c*point.z + d) / (a*direction.x + b*direction.y + c*direction.z);
		float x = point.x + t*direction.x;		
		float y = point.y + t*direction.y;		
		float z = point.z + t*direction.z;
		return new Vector3(x, y, z);
	}
}
