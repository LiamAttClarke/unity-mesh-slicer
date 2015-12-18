using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class MeshSlicer {
	public static GameObject uvCamera;
	private static int[] vertOrderCW = {0,3,4, 1,2,4, 4,3,1};
	private static int[] vertOrderCCW = {4,3,0, 4,2,1, 1,3,4};
	private static List<Vector3> slice1Verts, slice2Verts;
	private static List<int> slice1Tris, slice2Tris;
	private static List<Vector2> slice1UVs, slice2UVs;
	private static List<Line> lineLoop;
	
	// Slice mesh along plane intersection
	public static void SliceMesh(GameObject obj, CustomPlane plane, bool isConvex) {
		// original GameObject data
		Transform objTransform = obj.transform;
		Mesh objMesh = obj.GetComponent<MeshFilter>().mesh;
		Vector3[] meshVerts = objMesh.vertices;
		int[] meshTris = objMesh.triangles;
		Vector2[] meshUVs = objMesh.uv;
		
		// Slice mesh data
		slice1Verts = new List<Vector3>();
		slice2Verts = new List<Vector3>();
		slice1Tris = new List<int>();
		slice2Tris = new List<int>();
		slice1UVs = new List<Vector2>();
		slice2UVs = new List<Vector2>();
		lineLoop = new List<Line>();
		
		// Loop through triangles
		for(int i = 0; i < meshTris.Length / 3; i++) {
			// Define triangle 
			Vector3[] triVertsLocal = new Vector3[3];
			Vector3[] triVertsWorld = new Vector3[3];
			Vector2[] triUVs = new Vector2[3];
			for(int j = 0; j < 3; j++) {
				int meshIndexor = (i + 1) * 3 - (3 - j);
				triVertsLocal[j] = meshVerts[meshTris[meshIndexor]]; 					// local model space vertices
				triVertsWorld[j] = objTransform.TransformPoint(triVertsLocal[j]); 		// world space vertices
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
				Vector3 poi1 = plane.VectorPlanePOI(triVertsWorld2[0], (triVertsWorld2[1] - triVertsWorld2[0]).normalized);
				Vector3 poi2 = plane.VectorPlanePOI(triVertsWorld2[0], (triVertsWorld2[2] - triVertsWorld2[0]).normalized);
				slicedTriVerts[3] = objTransform.InverseTransformPoint(poi1);
				slicedTriVerts[4] = objTransform.InverseTransformPoint(poi2);

				lineLoop.Add(new Line(slicedTriVerts[3], slicedTriVerts[4], poi1, poi2));

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
				Vector3 normal = plane.normal;
				do {
					Polygon polygon = new Polygon(lineLoop, normal);
					Vector3 polygonNormal = (polygon.isClockWise) ? normal : -normal;
					TriangulatePolygon(polygon.edges, polygonNormal, polygon.isClockWise);
				} while(lineLoop.Count != 0);
			}
			// Build Meshes
			if(slice1Verts.Count > 0) {
				BuildSlice(obj, objTransform, slice1Verts.ToArray(), slice1Tris.ToArray(), slice1UVs.ToArray());
			}
			if(slice2Verts.Count > 0) {
				BuildSlice(obj, objTransform, slice2Verts.ToArray(), slice2Tris.ToArray(), slice2UVs.ToArray());
			}
			// Delete original
			GameObject.Destroy(obj);
		}
	}
	
	private static void TriangulatePolygon(List<Line> currentRing, Vector3 normal, bool isCW) {
        if (currentRing.Count == 1) {
            return;
        } else if (currentRing.Count <= 3) {
            AddTriangle(currentRing, slice1Verts, slice1Tris, slice1UVs, isCW, 0, normal);
            AddTriangle(currentRing, slice2Verts, slice2Tris, slice2UVs, !isCW, 0, normal);
        } else {
            List<Line> nextRing = new List<Line>();
            for (int i = 0; i < currentRing.Count; i++) {
                if (i == currentRing.Count - 1) {
                    nextRing.Add(currentRing[i]);
                    continue;
                }
                Vector3 cross = Vector3.Cross(currentRing[i].worldDir, currentRing[i + 1].worldDir).normalized;
                float side = Vector3.Dot(cross, normal);
                if (side >= 0) {
                    AddTriangle(currentRing, slice1Verts, slice1Tris, slice1UVs, isCW, i, normal);
                    AddTriangle(currentRing, slice2Verts, slice2Tris, slice2UVs, !isCW, i, normal);
                    nextRing.Add(new Line(currentRing[i].localPoint1, currentRing[i + 1].localPoint2, currentRing[i].worldPoint1, currentRing[i + 1].worldPoint2));
                    i++;
                } else { // left turn
                    nextRing.Add(currentRing[i]);
                }
            }
            // Overflow 
            if (nextRing.Count == currentRing.Count) {
                Debug.Log("Endless Recursion " + nextRing.Count);
                return;
            }
            // recurse
            TriangulatePolygon(nextRing, normal, isCW);
        }
	}
	
	private static void AddTriangle(List<Line> lineList, List<Vector3> vertList, List<int> triList, List<Vector2> uvList, bool isClockWise, int index, Vector3 normal) {
        if (lineList.Count < 2) {
            throw new UnityException("lineList must have at least 2 elements.");
        }
		if(isClockWise) {
			vertList.Add(lineList[index + 1].localPoint2);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index + 1].worldPoint2, normal ) );
			vertList.Add(lineList[index].localPoint2);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index].worldPoint2, normal ) );
			vertList.Add(lineList[index].localPoint1);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index].worldPoint1, normal ) );
		} else {
			vertList.Add(lineList[index].localPoint1);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index].worldPoint1, normal ) );
			vertList.Add(lineList[index].localPoint2);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index].worldPoint2, normal ) );
			vertList.Add(lineList[index + 1].localPoint2);
			triList.Add(vertList.Count - 1);
			uvList.Add( InnerUVCoord(lineList[index + 1].worldPoint2, normal ) );
		}
	}
	
	private static Vector2 InnerUVCoord(Vector3 vertex, Vector3 normal) {
		Quaternion alignmentRotation = Quaternion.FromToRotation(normal, -Camera.main.transform.forward);
		vertex = alignmentRotation * vertex;
		Camera cam = uvCamera.GetComponent<Camera>();
		Matrix4x4 matrixVP = cam.projectionMatrix * cam.worldToCameraMatrix;
		return matrixVP * vertex;
	}
	
	private static void BuildSlice(GameObject obj, Transform objTransform, Vector3[] vertices, int[] triangles, Vector2[] uv) {
		// Generate Mesh
		Mesh sliceMesh = new Mesh();
		sliceMesh.vertices = vertices;
		sliceMesh.triangles = triangles;
		sliceMesh.uv = uv;
		sliceMesh.RecalculateNormals();
		sliceMesh.RecalculateBounds();
		
		// Instantiate Slice and update properties
		GameObject slice = (GameObject)GameObject.Instantiate (obj, objTransform.position, objTransform.rotation);
		slice.name = obj.name;
		slice.GetComponent<MeshFilter>().mesh = sliceMesh;
		GameObject.Destroy(slice.GetComponent<Collider>());
		MeshCollider sliceMeshCollider = slice.AddComponent<MeshCollider>();
		sliceMeshCollider.sharedMesh = sliceMesh;
		sliceMeshCollider.convex = true;
		sliceMeshCollider.material = obj.GetComponent<Collider>().material;
		slice.GetComponent<Rigidbody>().velocity = obj.GetComponent<Rigidbody>().velocity;
	}
	
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
		// Test intersection of plane and object's bounding box
		public bool HitTest(GameObject obj) {
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
			float prevProduct = this.GetSide(boundingBoxVerts[0]);
			for(int i = 1; i < boundingBoxVerts.Length; i++) {
				float currentProduct = this.GetSide(boundingBoxVerts[i]);
				if (prevProduct * currentProduct < 0) {
					return true;
				}
				prevProduct = this.GetSide(boundingBoxVerts[i]);
			}
			return false;
		}
		// World-space point of intersection between vector this plane
		public Vector3 VectorPlanePOI(Vector3 point, Vector3 direction) {
			// Plane: Ax + By + Cz = D
			// Vector: r = (p.x, p.y, p.z) + t(d.x, d.y, d.z)
			float a = this.normal.x;
			float b = this.normal.y;
			float c = this.normal.z;
			float d = this.d;
			float t = -1 * (a*point.x + b*point.y + c*point.z + d) / (a*direction.x + b*direction.y + c*direction.z);
			float x = point.x + t*direction.x;		
			float y = point.y + t*direction.y;		
			float z = point.z + t*direction.z;
			return new Vector3(x, y, z);
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
	
	private struct Line {
		public readonly Vector3 localPoint1;
		public readonly Vector3 localPoint2;
		public readonly Vector3 worldPoint1;
		public readonly Vector3 worldPoint2;
		public readonly Vector3 worldDir;
		public Line(Vector3 localP1, Vector3 localP2, Vector3 worldP1, Vector3 worldP2) {
            localPoint1 = localP1;
            localPoint2 = localP2;
			worldPoint1 = worldP1;
			worldPoint2 = worldP2;
			worldDir = (worldP2 - worldP1).normalized;
		}
	}
	
	private struct Polygon {
		public readonly List<Line> edges;
		public readonly bool isClockWise;
		private List<Line> orderedList;
		public Polygon(List<Line> lineList, Vector3 normal) {
			this.orderedList = new List<Line>();
			this.orderedList.Add(lineList[0]);
			lineList.RemoveAt(0);
			this.edges = null;
			this.isClockWise = true;
			this.edges = this.OrderLineList(lineList);
			this.isClockWise = this.IsClockWise(normal);
		}
		private List<Line> OrderLineList(List<Line> lineList) {
			for(int i = 0; i < lineList.Count; i++) {
				if(orderedList[orderedList.Count - 1].localPoint2 == lineList[i].localPoint1) {
					this.orderedList.Add(lineList[i]);
					lineList.Remove(lineList[i]);
					i = lineList.Count;
					OrderLineList(lineList);
				} else if(orderedList[orderedList.Count - 1].localPoint2 == lineList[i].localPoint2) {
					this.orderedList.Add(new Line(lineList[i].localPoint2, lineList[i].localPoint1, 
												  lineList[i].worldPoint2, lineList[i].worldPoint1));
					lineList.Remove(lineList[i]);
					i = lineList.Count;
					OrderLineList(lineList);
				}
			}
			return this.orderedList;
		}
		// test if line segments are in opposite order
		private bool IsClockWise(Vector3 normal) {
			int leftTurns = 0;
			int rightTurns = 0;
			for(int i = 0; i < this.edges.Count - 1; i++) {
				Vector3 cross = Vector3.Cross(this.edges[i].worldDir, this.edges[i + 1].worldDir).normalized;
				float side = Vector3.Dot (cross, normal);
				if(side > 0) {
					rightTurns++;
				} else if(side < 0) {
					leftTurns++;
				}
			}
			return (leftTurns > rightTurns) ? false : true;
		}
	}
}