using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Slicer : MonoBehaviour {
	private Mesh objMesh;
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
				DetectIntersectingModels(plane);
			}
			isSlicing = false;
		}
	}
	
	void DetectIntersectingModels(Plane plane) {
		GameObject[] convexTargets = GameObject.FindGameObjectsWithTag("Sliceable-Convex");
		GameObject[] nonConvexTargets = GameObject.FindGameObjectsWithTag("Sliceable");
		foreach(GameObject target in convexTargets) {
			if(true) {
				SliceMesh(target, plane, true);
			}
		}
		foreach(GameObject target in nonConvexTargets) {
			if(true) {
				SliceMesh(target, plane, false);
			}
		}
	}
	
	// Slice mesh along plane intersection
	void SliceMesh(GameObject obj, Plane plane, bool isConvex) {
		// original
		Transform objTransform = obj.transform;
		Mesh objMesh = obj.GetComponent<MeshFilter>().mesh;
		Material objMaterial = obj.GetComponent<MeshRenderer>().material;
		Vector3[] meshVerts = objMesh.vertices;
		int[] meshTris = objMesh.triangles;
		Vector2[] meshUVs = objMesh.uv;
		// sliced
		List<Vector3> slice1Verts = new List<Vector3>();
		List<Vector3> slice2Verts = new List<Vector3>();
		List<int> slice1Tris = new List<int>();
		List<int> slice2Tris = new List<int>();
		List<Vector2> slice1UVs = new List<Vector2>();
		List<Vector2> slice2UVs = new List<Vector2>();
		int slice1TriCounter = 1;
		int slice2TriCounter = 1;
		
		int numOfTris = meshTris.Length / 3;
		List<Vector3> POIs = new List<Vector3>();
		// Loop through triangles
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
			Vector2[] triUVs = {
				meshUVs[meshTris[i * 3 - 3]],
				meshUVs[meshTris[i * 3 - 2]],
				meshUVs[meshTris[i * 3 - 1]]
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
					slice1UVs.Add(triUVs[j]);
				}
				slice1TriCounter++;
			} else if(prod1 < 0 && prod2 < 0 && prod3 < 0) { // Slice 2
				for(int j = 0; j < localVerts.Length; j++) {
					slice2Verts.Add(localVerts[j]);
					slice2Tris.Add(slice2TriCounter * 3 - (3 - j));
					slice2UVs.Add(triUVs[j]);
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
				if(isConvex) {
					POIs.Add(localTriVerts[3]);
					POIs.Add(localTriVerts[4]);
				}
				// Add bisected triangle to slice respectively
				if(Vector3.Dot(plane.normal, p1 - plane.point) > 0) {
					// Slice 1
					for(int j = 0; j < 3; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j));
						slice1UVs.Add(triUVs[triOrder[j] / 3 + triOrder[j] % 3]); // PLACEHOLDER
					}
					slice1TriCounter++;
					// Slice 2
					for(int j = 3; j < 6; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j % 3));
						slice2UVs.Add(triUVs[triOrder[j - 3] / 3 + triOrder[j - 3] % 3]); // PLACEHOLDER
					}
					slice2TriCounter++;
					for(int j = 6; j < 9; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j % 3));
						slice2UVs.Add(triUVs[triOrder[j - 6] / 3 + triOrder[j - 6] % 3]); // PLACEHOLDER
					}
					slice2TriCounter++;
				} else {
					// Slice 2
					for(int j = 0; j < 3; j++) {
						slice2Verts.Add(localTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2TriCounter * 3 - (3 - j));
						slice2UVs.Add(triUVs[triOrder[j] / 3 + triOrder[j] % 3]); // PLACEHOLDER
					}
					slice2TriCounter++;
					// Slice 1
					for(int j = 3; j < 6; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j % 3));
						slice1UVs.Add(triUVs[triOrder[j - 3] / 3 + triOrder[j - 3] % 3]); // PLACEHOLDER
					}
					slice1TriCounter++;
					for(int j = 6; j < 9; j++) {
						slice1Verts.Add(localTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1TriCounter * 3 - (3 - j % 3));
						slice1UVs.Add(triUVs[triOrder[j - 6] / 3 + triOrder[j - 6] % 3]); // PLACEHOLDER
					}
					slice1TriCounter++;
				}		
			}
		}
		// Fill convex mesh
		if(isConvex) {
			List<Vector3> filteredPOIs = POIs.Distinct().ToList();
			// fill mesh
		}
		// Build Meshes
		if(slice1Verts.Count > 0) {
			BuildSlice(slice1Verts.ToArray(), slice1Tris.ToArray(), slice1UVs.ToArray(), obj.transform, objMaterial, isConvex);
		}
		if(slice2Verts.Count > 0) {
			BuildSlice(slice2Verts.ToArray(), slice2Tris.ToArray(), slice2UVs.ToArray(), obj.transform, objMaterial, isConvex);
		}
		// Delete original
		Destroy(obj);
	}
	
	void BuildSlice(Vector3[] vertices, int[] triangles, Vector2[] uv, Transform objTransform, Material objMaterial, bool isConvex) {
		Mesh sliceMesh = new Mesh();
		sliceMesh.vertices = vertices;
		sliceMesh.triangles = triangles;
		sliceMesh.uv = uv;
		sliceMesh.RecalculateNormals();
		sliceMesh.RecalculateBounds();
		// Instantiate new gameObject with components
		GameObject slice = new GameObject("Slice");
		slice.AddComponent<MeshFilter>();
		slice.AddComponent<MeshRenderer>();
		// Assign values to gameObject
		slice.GetComponent<MeshFilter>().mesh = sliceMesh;
		slice.GetComponent<MeshRenderer>().material = objMaterial; 
		slice.transform.position = objTransform.position;
		slice.transform.rotation = objTransform.rotation;
		slice.transform.localScale = objTransform.localScale;
		if(isConvex) {
			slice.tag = "Sliceable-Convex";
		} else {
			slice.tag = "Sliceable";	
		}
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
