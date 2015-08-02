using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Slicer : MonoBehaviour {
	public bool fillConvexMesh = true;
	private Vector3 sliceStartPos, sliceEndPos;
	private bool isSlicing = false;
	private int[] vertOrderCW = {0,3,4, 1,2,4, 4,3,1};
	private int[] vertOrderCCW = {4,3,0, 4,2,1, 1,3,4};
	List<Vector3> slice1Verts, slice2Verts;
	List<int> slice1Tris, slice2Tris;
	List<Vector2> slice1UVs, slice2UVs;

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
		// Calculate "D" constant for the eqation of a plane (Ax + By + Cz = D)
		private float PlaneDConstant(Vector3 planeP, Vector3 planeN) {
			return -1.0f * ((planeN.x * planeP.x) + (planeN.y * planeP.y) + (planeN.z * planeP.z));
		}
		// Calculate plane's normal given 3 points on the plane
		private Vector3 PlaneNormal(Vector3 point1, Vector3 point2, Vector3 point3) {
			Vector3 vect1 = (point2 - point1);
			Vector3 vect2 = (point3 - point1);
			return Vector3.Cross(vect1, vect2).normalized;
		}
	}
	
	struct LineSegment {
		public readonly Vector3 localP1;
		public readonly Vector3 localP2;
		public readonly Vector3 localVect;
		public readonly Vector3 worldP1;
		public readonly Vector3 worldP2;
		public readonly Vector3 worldVect;
		public LineSegment(Vector3 localP1, Vector3 localP2, Vector3 worldP1, Vector3 worldP2) {
			this.localP1 = localP1;
			this.localP2 = localP2;
			this.worldP1 = worldP1;
			this.worldP2 = worldP2;
			this.localVect = (localP2 - localP1);
			this.worldVect = (worldP2 - worldP1);
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
			if(PlaneHitTest(target, plane)) {
				SliceMesh(target, plane, true);
			}
		}
		foreach(GameObject target in nonConvexTargets) {
			if(PlaneHitTest(target, plane)) {
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
		string objName = obj.name;
		
		// Slice mesh data
		slice1Verts = new List<Vector3>();
		slice2Verts = new List<Vector3>();
		slice1Tris = new List<int>();
		slice2Tris = new List<int>();
		slice1UVs = new List<Vector2>();
		slice2UVs = new List<Vector2>();
		List<LineSegment> lineLoop = new List<LineSegment>();
		
		// Loop through triangles
		for(int i = 0; i < meshTris.Length / 3; i++) {
			
			// Define triangle 
			Vector3[] triVertsLocal = new Vector3[3];
			Vector3[] triVertsWorld = new Vector3[3];
			Vector2[] triUVs = new Vector2[3];
			for(int j = 0; j < 3; j++) {
				int meshIndexor = (i + 1) * 3 - (3 - j);
				triVertsLocal[j] = meshVerts[meshTris[meshIndexor]]; 					// local model space vertices
				triVertsWorld[j] = objTransform.TransformPoint(triVertsLocal[j]); 	// world space vertices
				triUVs[j] = meshUVs[meshTris[meshIndexor]]; 							// original uv coordinates
			}

			// Side test: (0) = intersecting plane; (+) = above plane; (-) = below plane;
			float vert1Side = plane.GetSide(triVertsWorld[0]);
			float vert2Side = plane.GetSide(triVertsWorld[1]);
			float vert3Side = plane.GetSide(triVertsWorld[2]);
			
			// assign triangles that do not intersect plane
			if(vert1Side > 0 && vert2Side > 0 && vert3Side > 0) { 			// Slice 1
				for(int j = 0; j < triVertsLocal.Length; j++) {
					slice1Verts.Add(triVertsLocal[j]);
					slice1Tris.Add(slice1Verts.Count - 1);
					slice1UVs.Add(triUVs[j]);
				}
			} else if(vert1Side < 0 && vert2Side < 0 && vert3Side < 0) {	// Slice 2
				for(int j = 0; j < triVertsLocal.Length; j++) {
					slice2Verts.Add(triVertsLocal[j]);
					slice2Tris.Add(slice2Verts.Count - 1);
					slice2UVs.Add(triUVs[j]);
				}
			} else {														// Intersecting Triangles
				Vector3[] slicedTriVerts = new Vector3[5];
				Vector2[] slicedTriUVs = new Vector2[5];
				Vector3[] triVertsWorld2 = new Vector3[3];
				int[] vertOrder; // triangle vertex-order CW vs. CCW
				if(vert1Side * vert2Side > 0) {
					int[] triOrder = {2,0,1};
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCW;					
				} else if(vert1Side * vert3Side > 0) {
					int[] triOrder = {1,0,2};
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCCW;
				} else {
					int[] triOrder = {0,1,2};
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCW;
				}
				
				// Points of Intersection
				Vector3 poi1 = VectorPlanePOI(triVertsWorld2[0], (triVertsWorld2[1] - triVertsWorld2[0]).normalized, plane);
				Vector3 poi2 = VectorPlanePOI(triVertsWorld2[0], (triVertsWorld2[2] - triVertsWorld2[0]).normalized, plane);
				slicedTriVerts[3] = objTransform.InverseTransformPoint(poi1);
				slicedTriVerts[4] = objTransform.InverseTransformPoint(poi2);
				
				// Save POIs for cross-sectional face
				if(isConvex && fillConvexMesh) {
					lineLoop.Add(new LineSegment(slicedTriVerts[3], slicedTriVerts[4], poi1, poi2));
				}
				
				// POI UVs
				float t1 = Vector3.Distance(slicedTriVerts[0], slicedTriVerts[3]) / Vector3.Distance(slicedTriVerts[0], slicedTriVerts[1]);
				float t2 = Vector3.Distance(slicedTriVerts[0], slicedTriVerts[4]) / Vector3.Distance(slicedTriVerts[0], slicedTriVerts[2]);
				slicedTriUVs[3] = Vector2.Lerp(slicedTriUVs[0], slicedTriUVs[1], t1);
				slicedTriUVs[4] = Vector2.Lerp(slicedTriUVs[0], slicedTriUVs[2], t2);
				
				// Add bisected triangle to slice respectively
				if(plane.GetSide(triVertsWorld2[0]) > 0) {
					// Slice 1
					for(int j = 0; j < 3; j++) {
						slice1Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
					// Slice 2
					for(int j = 3; j < 6; j++) {
						slice2Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
					for(int j = 6; j < 9; j++) {
						slice2Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
				} else {
					// Slice 2
					for(int j = 0; j < 3; j++) {
						slice2Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice2Tris.Add(slice2Verts.Count - 1);
						slice2UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
					// Slice 1
					for(int j = 3; j < 6; j++) {
						slice1Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
					for(int j = 6; j < 9; j++) {
						slice1Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
				}
			}
		}
		// Fill convex mesh
		if(isConvex && fillConvexMesh) {
			FillSlice(lineLoop, plane.normal);
		}
		// Build Meshes
		if(slice1Verts.Count > 0) {
			BuildSlice(objName, slice1Verts.ToArray(), slice1Tris.ToArray(), slice1UVs.ToArray(), obj.transform, objMaterial, isConvex);
		}
		if(slice2Verts.Count > 0) {
			BuildSlice(objName, slice2Verts.ToArray(), slice2Tris.ToArray(), slice2UVs.ToArray(), obj.transform, objMaterial, isConvex);
		}
		// Delete original
		Destroy(obj);
	}
	
	void FillSlice(List<LineSegment> ring, Vector3 normal) {
		List<LineSegment> interiorRing = new List<LineSegment>();
		for(int i = 0; i < ring.Count - 2; i += 2) {
			/*if(ring[i].worldP2 == ring[0].worldP1 || ring[i + 1].worldP2 == ring[0].worldP1) {
				i = ring.Count;
				continue;
			}*/ //requires ordered line segments
			Vector3 cross = Vector3.Cross(ring[i].worldVect, ring[i + 1].worldVect).normalized;
			if(cross == normal) {
				slice1Verts.Add(ring[i].localP1);
				slice1Tris.Add(slice1Verts.Count - 1);
				slice1Verts.Add(ring[i].localP2);
				slice1Tris.Add(slice1Verts.Count - 1);
				slice1Verts.Add(ring[i + 1].localP2);
				slice1Tris.Add(slice1Verts.Count - 1);
				// uvs
				slice1UVs.Add(Vector2.zero);
				slice1UVs.Add(Vector2.zero);
				slice1UVs.Add(Vector2.zero);
				interiorRing.Add(new LineSegment(ring[i].localP1, ring[i+1].localP2, ring[i].worldP1, ring[i+1].worldP2));
			} else {
				interiorRing.Add(new LineSegment(ring[i].localP1, ring[i].localP2, ring[i].worldP1, ring[i].worldP2));
				i--;
			}
		}
		if(interiorRing.Count > 3) {
			FillSlice(interiorRing, normal);
			Debug.Log ("recursed");
		} else {
			slice1Verts.Add(interiorRing[0].localP1);
			slice1Tris.Add(slice1Verts.Count - 1);
			slice1Verts.Add(interiorRing[0].localP2);
			slice1Tris.Add(slice1Verts.Count - 1);
			slice1Verts.Add(interiorRing[1].localP2);
			slice1Tris.Add(slice1Verts.Count - 1);
			// uvs
			slice1UVs.Add(Vector2.zero);
			slice1UVs.Add(Vector2.zero);
			slice1UVs.Add(Vector2.zero);
		}
	}
	
	void BuildSlice(string name, Vector3[] vertices, int[] triangles, Vector2[] uv, Transform objTransform, Material objMaterial, bool isConvex) {
		Mesh sliceMesh = new Mesh();
		sliceMesh.vertices = vertices;
		sliceMesh.triangles = triangles;
		sliceMesh.uv = uv;
		sliceMesh.RecalculateNormals();
		sliceMesh.RecalculateBounds();
		// Instantiate new gameObject with components
		GameObject slice = new GameObject(name + "-Slice");
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
	bool PlaneHitTest(GameObject obj, CustomPlane plane) {
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
