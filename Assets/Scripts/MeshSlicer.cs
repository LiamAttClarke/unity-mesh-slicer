using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class MeshSlicer {
	private static int[] vertOrderCW = { 0,3,4, 1,2,4, 4,3,1 };
	private static int[] vertOrderCCW = { 4,3,0, 4,2,1, 1,3,4 };
	private static List<Vector3> slice1Verts, slice2Verts;
	private static List<int> slice1Tris, slice2Tris;
	private static List<Vector2> slice1UVs, slice2UVs;
	private static List<Line> lineLoop;
	
	public static void SliceMesh(GameObject obj, Plane plane) {
        //if (!IsPlaneIntersectingMesh(obj.GetComponent<MeshFilter>().mesh, plane)) {
        //    Debug.Log("No intersection here");
        //    return;
        //}

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
		// Iterate over mesh triangles
		for(int i = 0; i < meshTris.Length / 3; i++) {
			// Define triangle 
			Vector3[] triVertsLocal = new Vector3[3];
			Vector3[] triVertsWorld = new Vector3[3];
			Vector2[] triUVs = new Vector2[3];
			for(int j = 0; j < 3; j++) {
				int meshIndexor = (i + 1) * 3 - (3 - j);
				triVertsLocal[j] = meshVerts[meshTris[meshIndexor]];
				triVertsWorld[j] = objTransform.TransformPoint(triVertsLocal[j]);
				triUVs[j] = meshUVs[meshTris[meshIndexor]];
			}
			bool vert1Side = plane.GetSide(triVertsWorld[0]);
			bool vert2Side = plane.GetSide(triVertsWorld[1]);
			bool vert3Side = plane.GetSide(triVertsWorld[2]);
			if (vert1Side && vert2Side && vert3Side) {
                // Add triangle to slice 1
				for(int j = 0; j < triVertsLocal.Length; j++) {
					slice1Verts.Add(triVertsLocal[j]);
					slice1Tris.Add(slice1Verts.Count - 1);
					slice1UVs.Add(triUVs[j]);
				}
			} else if (!vert1Side && !vert2Side && !vert3Side) {
                // Add triangle to slice 2
				for(int j = 0; j < triVertsLocal.Length; j++) {
					slice2Verts.Add(triVertsLocal[j]);
					slice2Tris.Add(slice2Verts.Count - 1);
					slice2UVs.Add(triUVs[j]);
				}
			} else {
                // Split intersecting triangle
				Vector3[] slicedTriVerts = new Vector3[5];
				Vector2[] slicedTriUVs = new Vector2[5];
				Vector3[] triVertsWorld2 = new Vector3[3];
				int[] vertOrder; // triangle vertex-order CW vs. CCW
				if (vert1Side == vert2Side) {
					int[] triOrder = { 2, 0, 1 };
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCW;					
				} else if (vert1Side == vert3Side) {
					int[] triOrder = { 1, 0, 2 };
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCCW;
				} else {
					int[] triOrder = { 0, 1, 2 };
					for(int j = 0; j < triOrder.Length; j++) {
						slicedTriVerts[j] = triVertsLocal[triOrder[j]];
						slicedTriUVs[j] = triUVs[triOrder[j]];
						triVertsWorld2[j] = triVertsWorld[triOrder[j]];
					}
					vertOrder = vertOrderCW;
				}

                // Points of Intersection
                Ray poiRay1 = new Ray(triVertsWorld2[0], (triVertsWorld2[1] - triVertsWorld2[0]).normalized);
                Ray poiRay2 = new Ray(triVertsWorld2[0], (triVertsWorld2[2] - triVertsWorld2[0]).normalized);
                float rayDistance = 0;
                plane.Raycast(poiRay1, out rayDistance);
                Vector3 poi1 = poiRay1.origin + poiRay1.direction * rayDistance;
                plane.Raycast(poiRay2, out rayDistance);
                Vector3 poi2 = poiRay2.origin + poiRay2.direction * rayDistance;

                slicedTriVerts[3] = objTransform.InverseTransformPoint(poi1);
				slicedTriVerts[4] = objTransform.InverseTransformPoint(poi2);

				// POI UVs
				float t1 = Vector3.Distance(slicedTriVerts[0], slicedTriVerts[3]) / Vector3.Distance(slicedTriVerts[0], slicedTriVerts[1]);
				float t2 = Vector3.Distance(slicedTriVerts[0], slicedTriVerts[4]) / Vector3.Distance(slicedTriVerts[0], slicedTriVerts[2]);
				slicedTriUVs[3] = Vector2.Lerp(slicedTriUVs[0], slicedTriUVs[1], t1);
				slicedTriUVs[4] = Vector2.Lerp(slicedTriUVs[0], slicedTriUVs[2], t2);
				
				// Add bisected triangle to slice respectively
				if(plane.GetSide(triVertsWorld2[0])) {
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
        // Build Meshes
        if (slice1Verts.Count > 0) {
			BuildSlice(obj, slice1Verts.ToArray(), slice1Tris.ToArray(), slice1UVs.ToArray());
		}
		if(slice2Verts.Count > 0) {
			BuildSlice(obj, slice2Verts.ToArray(), slice2Tris.ToArray(), slice2UVs.ToArray());
		}
		// Delete original
		Object.Destroy(obj);
	}

    // TODO: Fix this it doesnt work...
    public static bool IsPlaneIntersectingMesh(Mesh mesh, Plane plane) {
        Vector3 objMin = mesh.bounds.min;
        Vector3 objMax = mesh.bounds.max;
        Vector3[] meshBoundVerts = {
            objMin,
            objMax,
            new Vector3(objMin.x, objMin.y, objMax.z),
            new Vector3(objMin.x, objMax.y, objMin.z),
            new Vector3(objMax.x, objMin.y, objMin.z),
            new Vector3(objMin.x, objMax.y, objMax.z),
            new Vector3(objMax.x, objMin.y, objMax.z),
            new Vector3(objMax.x, objMax.y, objMin.z)
        };
        bool side = plane.GetSide(meshBoundVerts[0]);
        for (int i = 1; i < meshBoundVerts.Length; i++) {
            if (plane.GetSide(meshBoundVerts[i]) != side) {
                return true;
            }
        }
        return false;
    }

    private static void TriangulatePolygon() {

    }

    private static void AddTriangle(List<Line> lineList, List<Vector3> vertList, List<int> triList, List<Vector2> uvList, bool isClockWise, int index, Vector3 normal) {
        if (lineList.Count < 2) {
            throw new UnityException("lineList must have at least 2 elements.");
        }
		if(isClockWise) {
			vertList.Add(lineList[index + 1].localPoint2);
			triList.Add(vertList.Count - 1);
            uvList.Add(Vector3.zero);
            vertList.Add(lineList[index].localPoint2);
			triList.Add(vertList.Count - 1);
            uvList.Add(Vector3.zero);
            vertList.Add(lineList[index].localPoint1);
			triList.Add(vertList.Count - 1);
            uvList.Add(Vector3.zero);
        } else {
			vertList.Add(lineList[index].localPoint1);
			triList.Add(vertList.Count - 1);
            uvList.Add(Vector3.zero);
            vertList.Add(lineList[index].localPoint2);
			triList.Add(vertList.Count - 1);
            uvList.Add(Vector3.zero);
            vertList.Add(lineList[index + 1].localPoint2);
			triList.Add(vertList.Count - 1);
			uvList.Add(Vector3.zero);
		}
	}

    private static void BuildSlice(GameObject obj, Vector3[] vertices, int[] triangles, Vector2[] uvs) {
        // Generate Mesh
        Mesh sliceMesh = new Mesh();
        sliceMesh.vertices = vertices;
        sliceMesh.triangles = triangles;
        sliceMesh.uv = uvs;
        sliceMesh.RecalculateNormals();
        sliceMesh.RecalculateBounds();
        // Instantiate slice and update properties
        GameObject slice = Object.Instantiate(obj);
        slice.GetComponent<MeshFilter>().mesh = sliceMesh;
        Collider sliceOriginalCollider = slice.GetComponent<Collider>();
        if (sliceOriginalCollider != null) {
            MeshCollider sliceCollider = slice.AddComponent<MeshCollider>();
            sliceCollider.sharedMesh = sliceMesh;
            sliceCollider.convex = true;
            sliceCollider.material = sliceOriginalCollider.material;
            Object.Destroy(sliceOriginalCollider);
        }
        Rigidbody sliceRigidbody = slice.GetComponent<Rigidbody>();
        if (sliceRigidbody) {
            sliceRigidbody.velocity = obj.GetComponent<Rigidbody>().velocity;
            // Compute new mesh center
            Vector3 centerOfMass = vertices[0];
            for (int i = 1; i < vertices.Length; i++) {
                centerOfMass += vertices[i];
            }
            centerOfMass /= vertices.Length;
            sliceRigidbody.centerOfMass = centerOfMass;
        }
    }    
}

struct Line {
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

//struct Polygon {
//    public readonly List<Line> edges;
//    public readonly bool isClockWise;
//    private List<Line> orderedList;

//    public Polygon(List<Line> lineList, Vector3 normal) {
//        this.orderedList = new List<Line>();
//        this.orderedList.Add(lineList[0]);
//        lineList.RemoveAt(0);
//        this.edges = null;
//        this.isClockWise = true;
//        this.edges = this.OrderLineList(lineList);
//        this.isClockWise = this.IsClockWise(normal);
//    }

//    private List<Line> OrderLineList(List<Line> lineList) {
//        for (int i = 0; i < lineList.Count; i++) {
//            if (orderedList[orderedList.Count - 1].localPoint2 == lineList[i].localPoint1) {
//                this.orderedList.Add(lineList[i]);
//                lineList.Remove(lineList[i]);
//                i = lineList.Count;
//                OrderLineList(lineList);
//            } else if (orderedList[orderedList.Count - 1].localPoint2 == lineList[i].localPoint2) {
//                this.orderedList.Add(new Line(lineList[i].localPoint2, lineList[i].localPoint1,
//                                                lineList[i].worldPoint2, lineList[i].worldPoint1));
//                lineList.Remove(lineList[i]);
//                i = lineList.Count;
//                OrderLineList(lineList);
//            }
//        }
//        return this.orderedList;
//    }

//    // test if line segments are in opposite order
//    private bool IsClockWise(Vector3 normal) {
//        int leftTurns = 0;
//        int rightTurns = 0;
//        for (int i = 0; i < this.edges.Count - 1; i++) {
//            Vector3 cross = Vector3.Cross(this.edges[i].worldDir, this.edges[i + 1].worldDir).normalized;
//            float side = Vector3.Dot(cross, normal);
//            if (side > 0) {
//                rightTurns++;
//            } else if (side < 0) {
//                leftTurns++;
//            }
//        }
//        return (leftTurns > rightTurns) ? false : true;
//    }
//}