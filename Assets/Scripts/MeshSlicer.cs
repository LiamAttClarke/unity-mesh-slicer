﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class MeshSlicer {
	public static GameObject slicePrefab;
	private static bool isCW = true;
	private static int[] vertOrderCW = {0,3,4, 1,2,4, 4,3,1};
	private static int[] vertOrderCCW = {4,3,0, 4,2,1, 1,3,4};
	private static List<Vector3> slice1Verts, slice2Verts;
	private static List<int> slice1Tris, slice2Tris;
	private static List<Vector2> slice1UVs, slice2UVs;
	private static List<LineSegment> orderedList;
	private static List<LineSegment> nextRing = new List<LineSegment>();
	
	public struct CustomPlane {
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
			return -((planeN.x * planeP.x) + (planeN.y * planeP.y) + (planeN.z * planeP.z));
		}
		// Calculate plane's normal given 3 points on the planeF
		private Vector3 PlaneNormal(Vector3 point1, Vector3 point2, Vector3 point3) {
			Vector3 vect1 = (point2 - point1);
			Vector3 vect2 = (point3 - point2);
			return Vector3.Cross(vect2, vect1).normalized;
		}
	}
	
	private struct LineSegment {
		public readonly Vector3 localP1;
		public readonly Vector3 localP2;
		public readonly Vector3 worldP1;
		public readonly Vector3 worldP2;
		public readonly Vector3 worldDir;
		public LineSegment(Vector3 localP1, Vector3 localP2, Vector3 worldP1, Vector3 worldP2) {
			this.localP1 = localP1;
			this.localP2 = localP2;
			this.worldP1 = worldP1;
			this.worldP2 = worldP2;
			this.worldDir = (worldP2 - worldP1).normalized;
		}
	}
	
	// Detect & slice "sliceable" GameObjects whose bounding box intersects slicing plane
	public static void Slice(CustomPlane plane) {
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
	private static void SliceMesh(GameObject obj, CustomPlane plane, bool isConvex) {
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
			} else if(vert1Side == 0 || vert2Side == 0 || vert3Side == 0) {
				Debug.Log ("Point-Plane Intersection");
				return;
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

				lineLoop.Add(new LineSegment(slicedTriVerts[3], slicedTriVerts[4], poi1, poi2));

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
					for(int j = 3; j < 9; j++) {
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
					for(int j = 3; j < 9; j++) {
						slice1Verts.Add(slicedTriVerts[vertOrder[j]]);
						slice1Tris.Add(slice1Verts.Count - 1);
						slice1UVs.Add(slicedTriUVs[vertOrder[j]]);
					}
				}
			}
		}
		if(lineLoop.Count > 0) {
			// Fill convex mesh
			if(isConvex) {
				FillFace(lineLoop, plane.normal);
			}
			// Build Meshes
			if(slice1Verts.Count > 0) {
				BuildSlice(objName, slice1Verts.ToArray(), slice1Tris.ToArray(), slice1UVs.ToArray(), obj.transform, objMaterial, isConvex);
			}
			if(slice2Verts.Count > 0) {
				BuildSlice(objName, slice2Verts.ToArray(), slice2Tris.ToArray(), slice2UVs.ToArray(), obj.transform, objMaterial, isConvex);
			}
			// Delete original
			GameObject.Destroy(obj);
		}
	}
	
	private static void FillFace(List<LineSegment> ring, Vector3 normal) {
		isCW = true;
		orderedList = new List<LineSegment>();
		orderedList.Add(ring[0]);
		ring.RemoveAt(0);
		ring = OrderSegments (ring);
		// test if line segments are in opposite order
		int rightTurns = 0;
		int leftTurns = 0;
		for(int i = 0; i < ring.Count - 1; i++) {
			Vector3 cross = Vector3.Cross(ring[i].worldDir, ring[i + 1].worldDir).normalized;
			float side = Vector3.Dot (cross, normal);
			if(side > 0) {
				rightTurns++;
			} else if(side < 0) {
				leftTurns++;
			}
		}
		if(leftTurns > rightTurns) {
			// reverse order
			normal *= -1f;
			isCW = false;
		}
		TriangulatePolygon(ring, normal);
	}

	private static List<LineSegment> OrderSegments(List<LineSegment> lineLoop) {
		for(int i = 0; i < lineLoop.Count; i++) {
			if(orderedList[orderedList.Count - 1].localP2 == lineLoop[i].localP1) {
				orderedList.Add(lineLoop[i]);
				lineLoop.Remove(lineLoop[i]);
				OrderSegments(lineLoop);
				i = lineLoop.Count;
			} else if(orderedList[orderedList.Count - 1].localP2 == lineLoop[i].localP2) {
				LineSegment flippedSegment = new LineSegment(lineLoop[i].localP2, lineLoop[i].localP1, 
				                                             lineLoop[i].worldP2, lineLoop[i].worldP1);
				lineLoop.Remove(lineLoop[i]);
				orderedList.Add(flippedSegment);
				OrderSegments(lineLoop);
				i = lineLoop.Count;
			}
		}

		return orderedList;
	}
	
	private static void TriangulatePolygon(List<LineSegment> previousRing, Vector3 normal) {
		nextRing.Clear ();
		for(int i = 0; i < previousRing.Count; i++) {
			if(i == previousRing.Count - 1) {
				nextRing.Add(previousRing[i]);
				i = previousRing.Count;
				continue;
			}
			Vector3 cross = Vector3.Cross(previousRing[i].worldDir, previousRing[i + 1].worldDir).normalized;
			float side = Vector3.Dot (cross, normal);
			if(side > 0) {
				AddTriangle(previousRing, slice1Verts, slice1Tris, slice1UVs, isCW, i);
				AddTriangle(previousRing, slice2Verts, slice2Tris, slice2UVs, !isCW, i);
				nextRing.Add(new LineSegment(previousRing[i].localP1, previousRing[i+1].localP2, previousRing[i].worldP1, previousRing[i+1].worldP2));
				i++;
			} else if(side == 0) {
				nextRing.Add(new LineSegment(previousRing[i].localP1, previousRing[i+1].localP2, previousRing[i].worldP1, previousRing[i+1].worldP2));
			} else {
				nextRing.Add(previousRing[i]);
			}
		}
		if(nextRing.Count == previousRing.Count) {
			Debug.Log ("Overflow");
			return; 
		}
		if(nextRing.Count > 3) {
			TriangulatePolygon(new List<LineSegment>(nextRing), normal);
		} else if (nextRing.Count == 3) {
			AddTriangle(nextRing, slice1Verts, slice1Tris, slice1UVs, isCW, 0);
			AddTriangle(nextRing, slice2Verts, slice2Tris, slice2UVs, !isCW, 0);
		} else {
			//
		}
	}
	
	private static void AddTriangle(List<LineSegment> lineList, List<Vector3> vertList, List<int> triList, List<Vector2> uvList, bool isClockWise, int index) {
		if(isClockWise) {
			vertList.Add(lineList[index + 1].localP2);
			triList.Add(vertList.Count - 1);
			vertList.Add(lineList[index].localP2);
			triList.Add(vertList.Count - 1);
			vertList.Add(lineList[index].localP1);
			triList.Add(vertList.Count - 1);
			
			uvList.Add(Vector2.zero);
			uvList.Add(Vector2.zero);
			uvList.Add(Vector2.zero);
		} else {
			vertList.Add(lineList[index].localP1);
			triList.Add(vertList.Count - 1);
			vertList.Add(lineList[index].localP2);
			triList.Add(vertList.Count - 1);
			vertList.Add(lineList[index + 1].localP2);
			triList.Add(vertList.Count - 1);

			uvList.Add(Vector2.zero);
			uvList.Add(Vector2.zero);
			uvList.Add(Vector2.zero);	
		}
	}
	
	private static void BuildSlice(string name, Vector3[] vertices, int[] triangles, Vector2[] uv, Transform objTransform, Material objMaterial, bool isConvex) {
		// Generate Mesh
		Mesh sliceMesh = new Mesh();
		sliceMesh.vertices = vertices;
		sliceMesh.triangles = triangles;
		sliceMesh.uv = uv;
		sliceMesh.RecalculateNormals();
		sliceMesh.RecalculateBounds();
		// Instantiate Slice and update properties
		GameObject slice = (GameObject)GameObject.Instantiate (slicePrefab, objTransform.position, objTransform.rotation);
		slice.name = name + "-Slice";
		slice.GetComponent<MeshFilter>().mesh = sliceMesh;
		slice.GetComponent<MeshCollider>().sharedMesh = sliceMesh;
		slice.transform.localScale = objTransform.localScale;
		slice.GetComponent<MeshRenderer> ().material = objMaterial;
		if(isConvex) {
			slice.tag = "Sliceable-Convex";
		} else {
			slice.tag = "Sliceable";	
		}
		//experimental algorithm
		var mc = slice.GetComponent<MeshCollider>();
		mc.convex = !mc.convex;
		mc.convex = !mc.convex;
	}

	// Test intersection of plane and object's bounding box
	private static bool PlaneHitTest(GameObject obj, CustomPlane plane) {
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
	private static Vector3 VectorPlanePOI(Vector3 point, Vector3 direction, CustomPlane plane) {
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