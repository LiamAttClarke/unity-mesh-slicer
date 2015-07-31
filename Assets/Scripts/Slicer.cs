using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Slicer : MonoBehaviour {
	private Vector3 sliceStartPos, sliceEndPos;
	private bool isSlicing = false;
	struct CustomPlane {
		public readonly Vector3 point;
		public readonly Vector3 normal;
		public readonly float d;
		// Plane given 3 points
		public CustomPlane(Vector3 point1, Vector3 point2, Vector3 point3) {
			this.point = point1;
			this.normal = Vector3.zero;
			this.d = 0;
			this.normal = PlaneNormal(point1, point2, point3);
			this.d = PlaneDConstant(point1, this.normal);
		}
		// Side test: (0) = intersecting plane; (+) = above plane; (-) = below plane;
		public float GetSide(Vector3 point) {
			return Vector3.Dot(this.normal, point - this.point);
		}
		// Calculate "D" value for the eqation of a plane "Ax + By + Cz = D"
		private float PlaneDConstant(Vector3 planeP, Vector3 planeN) {
			return -1.0f * ((planeN.x * planeP.x) + (planeN.y * planeP.y) + (planeN.z * planeP.z));
		}
		// Calculate plane's normal given 3 points on the plane
		private Vector3 PlaneNormal(Vector3 point1, Vector3 point2, Vector3 point3) {
			Vector3 vec1 = (point2 - point1).normalized;
			Vector3 vec2 = (point3 - point1).normalized;
			return Vector3.Cross(vec1, vec2);
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
				CustomPlane plane = new CustomPlane(sliceStartPos, sliceEndPos, point3);
				Slice(plane);
			}
			isSlicing = false;
		}
	}
	
	// Detect & slice "sliceable" GameObjects whose bounding box intersects slicing plane
	void Slice(CustomPlane plane) {
		GameObject[] convexTargets = GameObject.FindGameObjectsWithTag("Sliceable-Convex");
		GameObject[] nonConvexTargets = GameObject.FindGameObjectsWithTag("Sliceable");
		foreach(GameObject target in convexTargets) {
			if(IsPlaneIntersecting(target, plane)) {
				SliceMesh(target, plane, true);
			}
		}
		foreach(GameObject target in nonConvexTargets) {
			if(IsPlaneIntersecting(target, plane)) {
				SliceMesh(target, plane, false);
			}
		}
	}
	
	// Slice mesh along plane intersection
	void SliceMesh(GameObject obj, CustomPlane plane, bool isConvex) {
		
		// original GameObject data
		Transform objTransform = obj.transform;
		Mesh objMesh = obj.GetComponent<MeshFilter>().mesh;
		Material objMaterial = obj.GetComponent<MeshRenderer>().material;
		Vector3[] meshVerts = objMesh.vertices;
		int[] meshTris = objMesh.triangles;
		Vector2[] meshUVs = objMesh.uv;
		
		// Slice mesh data
		List<Vector3> slice1Verts = new List<Vector3>();
		List<Vector3> slice2Verts = new List<Vector3>();
		List<int> slice1Tris = new List<int>();
		List<int> slice2Tris = new List<int>();
		List<Vector2> slice1UVs = new List<Vector2>();
		List<Vector2> slice2UVs = new List<Vector2>();
		
		List<Vector3> POIs = new List<Vector3>();
		// Loop through triangles
		for(int i = 1; i <= meshTris.Length / 3; i++) {
			
			// Define triangle
			Vector3[] localTriVerts = new Vector3[3];
			Vector3[] worldTriVerts = new Vector3[3];
			Vector2[] triUVs = new Vector2[3];
			//int[] vertOrder = new int[3];
			for(int j = 0; j < 3; j++) {
				//int indexor = i + j;
				int indexor = i * 3 - (3 - j);
				localTriVerts[j] = meshVerts[meshTris[indexor]]; 					// local model space vertices
				worldTriVerts[j] = objTransform.TransformPoint(localTriVerts[j]); 	// world space vertices
				triUVs[j] = meshUVs[meshTris[indexor]]; 							// original uv coordinates
				//vertOrder[j] = meshTris[indexor];
			}

			// Side test: (0) = intersecting plane; (+) = above plane; (-) = below plane;
			float vert1Side = plane.GetSide(worldTriVerts[0]);
			float vert2Side = plane.GetSide(worldTriVerts[1]);
			float vert3Side = plane.GetSide(worldTriVerts[2]);
			
			// assign triangles that do not intersect plane
			if(vert1Side > 0 && vert2Side > 0 && vert3Side > 0) { 			// Slice 1
				for(int j = 0; j < localTriVerts.Length; j++) {
					slice1Verts.Add(localTriVerts[j]);
					slice1Tris.Add(slice1Verts.Count - 1);
					slice1UVs.Add(triUVs[j]);
				}
			} else if(vert1Side < 0 && vert2Side < 0 && vert3Side < 0) {	// Slice 2
				for(int j = 0; j < localTriVerts.Length; j++) {
					slice2Verts.Add(localTriVerts[j]);
					slice2Tris.Add(slice2Verts.Count - 1);
					slice2UVs.Add(triUVs[j]);
				}
			} else {														// Intersecting Triangles
				// Determine which line segment intersects plane
				Vector3 p1, p2, p3;
				Vector2 uv1, uv2, uv3;
				int[] triOrder; // triangle vertex-order CW vs. CCW
				if(vert1Side * vert2Side > 0) {
					p1 = worldTriVerts[2];
					p2 = worldTriVerts[0];
					p3 = worldTriVerts[1];
					uv1 = triUVs[2];
					uv2 = triUVs[0];
					uv3 = triUVs[1];
					triOrder = new int[] {0,3,4, 1,2,4, 4,3,1}; // CW
				} else if(vert1Side * vert3Side > 0) {
					p1 = worldTriVerts[1];
					p2 = worldTriVerts[0];
					p3 = worldTriVerts[2];
					uv1 = triUVs[1];
					uv2 = triUVs[0];
					uv3 = triUVs[2];
					triOrder = new int[] {4,3,0, 4,2,1, 1,3,4}; // CCW
				} else {
					p1 = worldTriVerts[0];
					p2 = worldTriVerts[1];
					p3 = worldTriVerts[2];
					uv1 = triUVs[0];
					uv2 = triUVs[1];
					uv3 = triUVs[2];
					triOrder = new int[] {0,3,4, 1,2,4, 4,3,1}; // CW
				}
				
				// bisected triangle vertices (local space)
				Vector3[] bisectedTriVerts = {
					objTransform.InverseTransformPoint(p1), //singleton
					objTransform.InverseTransformPoint(p2), // replace with precalculated localverts
					objTransform.InverseTransformPoint(p3),
					objTransform.InverseTransformPoint(VectorPlanePOI(p1, (p2 - p1).normalized, plane)), // 3
					objTransform.InverseTransformPoint(VectorPlanePOI(p1, (p3 - p1).normalized, plane)) // 4
				};
				
				// UV coordinates
				float t1 = Vector3.Distance(bisectedTriVerts[0], bisectedTriVerts[3]) / Vector3.Distance(bisectedTriVerts[0], bisectedTriVerts[1]);
				float t2 = Vector3.Distance(bisectedTriVerts[0], bisectedTriVerts[4]) / Vector3.Distance(bisectedTriVerts[0], bisectedTriVerts[2]);
				Vector2[] bisectedTriUVs = {
					uv1,
					uv2,
					uv3,
					Vector2.Lerp(uv1, uv2, t1),
					Vector2.Lerp(uv1, uv3, t2)
				};
				
				// Add bisected triangle to slice respectively
				if(plane.GetSide(p1) > 0) {
					// Slice 1
					for(int j = 0; j < 3; j++) {
						slice1Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
					// Slice 2
					for(int j = 3; j < 6; j++) {
						slice2Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
					for(int j = 6; j < 9; j++) {
						slice2Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
				} else {
					// Slice 2
					for(int j = 0; j < 3; j++) {
						slice2Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
					// Slice 1
					for(int j = 3; j < 6; j++) {
						slice1Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
					for(int j = 6; j < 9; j++) {
						slice1Verts.Add(bisectedTriVerts[triOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(bisectedTriUVs[triOrder[j]]);
					}
				}
				
				// Save POIs for cross-sectional face
				if(isConvex) {
					POIs.Add(bisectedTriVerts[3]);
					POIs.Add(bisectedTriVerts[4]);
				}	
			}
		}
		// Fill convex mesh
		if(isConvex) {
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
		slice.AddComponent<MeshCollider>();
		//slice.AddComponent<Rigidbody>();
		// Assign values to gameObject
		slice.GetComponent<MeshFilter>().mesh = sliceMesh;
		slice.GetComponent<MeshRenderer>().material = objMaterial; 
		slice.GetComponent<MeshCollider>().sharedMesh = sliceMesh;
		slice.transform.position = objTransform.position;
		slice.transform.rotation = objTransform.rotation;
		slice.transform.localScale = objTransform.localScale;
		if(isConvex) {
			slice.tag = "Sliceable-Convex";
		} else {
			slice.tag = "Sliceable";	
		}
	}

	// Test intersection of plane and object's bounding box
	bool IsPlaneIntersecting(GameObject obj, CustomPlane plane) {
		// test plane intersection against bounding box of Renderer.Bounds
		Vector3 objMax = obj.GetComponent<MeshRenderer>().bounds.max;
		Vector3 objMin = obj.GetComponent<MeshRenderer>().bounds.min;
		Vector3[] boundingBoxVerts = {
			objMin,
			objMax,
			new Vector3(objMin.x, objMin.y, objMax.z),
			new Vector3(objMin.x, objMax.y, objMin.z),
			new Vector3(objMax.x, objMin.y, objMin.z),
			new Vector3(objMin.x, objMax.y, objMax.z),
			new Vector3(objMax.x, objMin.y, objMax.z),
			new Vector3(objMax.x, objMax.y, objMin.z)
		};
		float prevProduct = plane.GetSide(boundingBoxVerts[0]);
		for(int i = 1; i < boundingBoxVerts.Length; i++) {
			float currentProduct = plane.GetSide(boundingBoxVerts[i]);
			if (prevProduct * currentProduct < 0) {
				return true;
			}
			prevProduct = plane.GetSide(boundingBoxVerts[i]);
		}
		return false;
	}
	
	// Point of intersection between vector and a plane
	Vector3 VectorPlanePOI(Vector3 point, Vector3 direction, CustomPlane plane) {
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
