using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FriedRice.Core
{
    public sealed class ShapeRenderer : IRenderer, IDisposable
    {
        private readonly List<Vector3> vertices;
        private readonly List<Vector2> uvs;
        private readonly List<Color> colors;
        private readonly List<int> triangles;
        private readonly List<RenderBatch> batches;
        private readonly List<SubMeshDescriptor> subMeshDescriptors;
        private int currentVertexCount;
        private int currentTriangleCount;
        private int currentBatchCount;
        private Mesh mesh;
        private Material material;
        private Texture2D defaultTexture;

        public ShapeRenderer()
        {
            vertices = new List<Vector3>();
            uvs = new List<Vector2>();
            colors = new List<Color>();
            triangles = new List<int>();
            batches = new List<RenderBatch>();
            subMeshDescriptors = new List<SubMeshDescriptor>();

            currentVertexCount = 0;
            currentTriangleCount = 0;
            currentBatchCount = 0;

            mesh = new Mesh();
            mesh.MarkDynamic();

            material = new Material(Shader.Find("FriedRice/ShapeShader"));
            defaultTexture = new Texture2D(2, 2);
            Color[] pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
            defaultTexture.SetPixels(pixels);
            defaultTexture.Apply(false);
        }

        public void Dispose()
        {
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        public void NewFrame()
        {
            // Reset the pointers.
            currentVertexCount = 0;
            currentTriangleCount = 0;
            currentBatchCount = 0;
        }

        public void EndFrame()
        {
            mesh.Clear();

            if (currentVertexCount > 0)
            {
                mesh.SetVertices(vertices, 0, currentVertexCount);
                mesh.SetUVs(0, uvs, 0, currentVertexCount);
                mesh.SetColors(colors, 0, currentVertexCount);
                mesh.SetTriangles(triangles, 0, currentTriangleCount, 0);

                EnsureSubMeshCapacity(currentBatchCount);

                for (int i = 0; i < currentBatchCount; i++)
                {
                    RenderBatch batch = batches[i];
                    SubMeshDescriptor desc = new SubMeshDescriptor
                    {
                        indexStart = batch.triangleStart,
                        indexCount = batch.triangleCount,
                        topology = MeshTopology.Triangles,
                        baseVertex = 0
                    };
                    subMeshDescriptors[i] = desc;
                }

                mesh.subMeshCount = currentBatchCount;
                mesh.SetSubMeshes(subMeshDescriptors, 0, currentBatchCount);
                mesh.RecalculateBounds();
            }
        }

        public void Draw()
        {
            if (mesh == null || mesh.vertexCount == 0 || material == null || currentBatchCount == 0)
                return;

            int mainTexId = Shader.PropertyToID("_MainTex");
            int clipRectId = Shader.PropertyToID("_ClipRect");

            Texture2D lastTexture = null;

            for (int i = 0; i < currentBatchCount; i++)
            {
                RenderBatch batch = batches[i];

                if (lastTexture != batch.texture)
                {
                    lastTexture = batch.texture;
                    material.SetTexture(mainTexId, batch.texture);
                }
                
                Rect rect = batch.clippingRect;
                Vector4 clippingRect = new Vector4(rect.x, rect.y, rect.width, rect.height);
                material.SetVector(clipRectId, clippingRect);

                material.SetPass(0);

                UnityEngine.Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
            }
        }

        public void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, float angleDegrees, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            EnsureListCapacity(currentVertexCount + 3, currentTriangleCount + 3);

            int batchTriangleStart = currentTriangleCount;

            // Convert raw input coordinates to top-down screen space
            Vector3 v0 = new Vector3(p1.x, Screen.height - p1.y, 0f);
            Vector3 v1 = new Vector3(p2.x, Screen.height - p2.y, 0f);
            Vector3 v2 = new Vector3(p3.x, Screen.height - p3.y, 0f);

            if (Mathf.Abs(angleDegrees) > 0.0001f)
            {
                // Calculate centroid to serve as the local rotation origin
                Vector3 center = (v0 + v1 + v2) / 3f;

                float rad = angleDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                // Rotate v0 around center
                float tx0 = v0.x - center.x;
                float ty0 = v0.y - center.y;
                v0.x = center.x + (tx0 * cos - ty0 * sin);
                v0.y = center.y + (tx0 * sin + ty0 * cos);

                // Rotate v1 around center
                float tx1 = v1.x - center.x;
                float ty1 = v1.y - center.y;
                v1.x = center.x + (tx1 * cos - ty1 * sin);
                v1.y = center.y + (tx1 * sin + ty1 * cos);

                // Rotate v2 around center
                float tx2 = v2.x - center.x;
                float ty2 = v2.y - center.y;
                v2.x = center.x + (tx2 * cos - ty2 * sin);
                v2.y = center.y + (tx2 * sin + ty2 * cos);
            }

            vertices[currentVertexCount + 0] = v0;
            vertices[currentVertexCount + 1] = v1;
            vertices[currentVertexCount + 2] = v2;

            uvs[currentVertexCount + 0] = new Vector2(0.5f, 1f);
            uvs[currentVertexCount + 1] = new Vector2(0f, 0f);
            uvs[currentVertexCount + 2] = new Vector2(1f, 0f);

            Color linearColor = color.linear;
            colors[currentVertexCount + 0] = linearColor;
            colors[currentVertexCount + 1] = linearColor;
            colors[currentVertexCount + 2] = linearColor;

            triangles[currentTriangleCount + 0] = currentVertexCount + 0;
            triangles[currentTriangleCount + 1] = currentVertexCount + 1;
            triangles[currentTriangleCount + 2] = currentVertexCount + 2;

            currentVertexCount += 3;
            currentTriangleCount += 3;

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                EnsureBatchCapacity(currentBatchCount + 1);

                batches[currentBatchCount] = new RenderBatch
                {
                    texture = texture == null ? defaultTexture : texture,
                    triangleStart = batchTriangleStart,
                    triangleCount = batchTriangleCount,
                    clippingRect = clippingRect
                };

                currentBatchCount++;
            }
        }

        public void DrawRectangle(Rect rect, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            // Ensure internal list capacities are expanded before index mapping access
            EnsureListCapacity(currentVertexCount + 4, currentTriangleCount + 6);

            int batchTriangleStart = currentTriangleCount;

            float xpos = rect.x;
            float ypos = Screen.height - rect.y;
            float w = rect.width;
            float h = rect.height;

            // Map rectangle bounds to vertex geometry positions
            vertices[currentVertexCount + 0] = new Vector3(xpos + w, ypos - h, 0);
            vertices[currentVertexCount + 1] = new Vector3(xpos, ypos - h, 0);
            vertices[currentVertexCount + 2] = new Vector3(xpos, ypos, 0);
            vertices[currentVertexCount + 3] = new Vector3(xpos + w, ypos, 0);

            // Map standardized 0-1 UV coordinate bounds for full texture display
            uvs[currentVertexCount + 0] = new Vector2(1f, 0f);
            uvs[currentVertexCount + 1] = new Vector2(0f, 0f);
            uvs[currentVertexCount + 2] = new Vector2(0f, 1f);
            uvs[currentVertexCount + 3] = new Vector2(1f, 1f);

            colors[currentVertexCount + 0] = color.linear;
            colors[currentVertexCount + 1] = color.linear;
            colors[currentVertexCount + 2] = color.linear;
            colors[currentVertexCount + 3] = color.linear;

            triangles[currentTriangleCount + 0] = currentVertexCount + 0;
            triangles[currentTriangleCount + 1] = currentVertexCount + 2;
            triangles[currentTriangleCount + 2] = currentVertexCount + 1;

            triangles[currentTriangleCount + 3] = currentVertexCount + 0;
            triangles[currentTriangleCount + 4] = currentVertexCount + 3;
            triangles[currentTriangleCount + 5] = currentVertexCount + 2;

            currentVertexCount += 4;
            currentTriangleCount += 6;

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                // Ensure the batch pool has internal list elements allocated
                EnsureBatchCapacity(currentBatchCount + 1);

                // FIX: Overwrite directly via structural index tracking instead of calling .Add()
                batches[currentBatchCount] = new RenderBatch
                {
                    texture = texture == null ? defaultTexture : texture,
                    triangleStart = batchTriangleStart,
                    triangleCount = batchTriangleCount,
                    clippingRect = clippingRect
                };

                currentBatchCount++;
            }
        }

        public void DrawRectangleEx(Rect rect, float radius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            DrawRectangleEx(rect, radius, radius, radius, radius, radius, color, texture, clippingRect);
        }

        public void DrawRectangleEx(Rect rect, float radius, float bottomLeftRadius, float bottomRightRadius, float topLeftRadius, float topRightRadius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            float roundEdges = 1.0f * radius;

            //Too lazy to sort this out
            float roundTopLeft = bottomLeftRadius;
            float roundTopRight = bottomRightRadius;
            float roundBottomLeft = topLeftRadius;
            float roundBottomRight = topRightRadius;
            bool usePercentage = false;
            const int cornerVertexCount = 16;

            int totalVertices = (cornerVertexCount * 4) + 1;
            int totalIndices = cornerVertexCount * 4 * 3;
            int batchTriangleStart = currentTriangleCount;

            EnsureListCapacity(currentVertexCount + totalVertices, currentTriangleCount + totalIndices);

            int count = cornerVertexCount * 4;

            float bl = Mathf.Max(0.0f, roundBottomLeft);
            float br = Mathf.Max(0.0f, roundBottomRight);
            float tl = Mathf.Max(0.0f, roundTopLeft);
            float tr = Mathf.Max(0.0f, roundTopRight);
            float f = (float)(Mathf.PI * 0.5f / Mathf.Max(1, cornerVertexCount - 1));
            float a1 = 1.0f;
            float a2 = 1.0f;
            float x = 1.0f;
            float y = 1.0f;
            Vector2 rs = new Vector2(1, 1);

            if (usePercentage)
            {
                rs = new Vector2(rect.width, rect.height) * 0.5f;
                if (rect.width > rect.height)
                {
                    a1 = rect.height / rect.width;
                }
                else
                {
                    a2 = rect.width / rect.height;
                }
                bl = Mathf.Clamp(bl, 0.0f, 1.0f);
                br = Mathf.Clamp(br, 0.0f, 1.0f);
                tl = Mathf.Clamp(tl, 0.0f, 1.0f);
                tr = Mathf.Clamp(tr, 0.0f, 1.0f);
            }
            else
            {
                x = rect.width * 0.5f;
                y = rect.height * 0.5f;
                if (bl + br > rect.width)
                {
                    float b = rect.width / (bl + br);
                    bl *= b;
                    br *= b;
                }
                if (tl + tr > rect.width)
                {
                    float b = rect.width / (tl + tr);
                    tl *= b;
                    tr *= b;
                }
                if (bl + tl > rect.height)
                {
                    float b = rect.height / (bl + tl);
                    bl *= b;
                    tl *= b;
                }
                if (br + tr > rect.height)
                {
                    float b = rect.height / (br + tr);
                    br *= b;
                    tr *= b;
                }
            }

            // Invert the center coordinate to top-down screen space
            Vector2 rectCenter = rect.center;
            Vector2 invertedCenter = new Vector2(rectCenter.x, Screen.height - rectCenter.y);
            int centerVertexIndex = currentVertexCount;

            vertices[centerVertexIndex] = invertedCenter;
            uvs[centerVertexIndex] = new Vector2(1, 1) * 0.5f;
            colors[centerVertexIndex] = color.linear;

            int perimeterStartVertex = centerVertexIndex + 1;

        for (int i = 0; i < cornerVertexCount; i++)
        {
            float s = Mathf.Sin((float)i * f);
            float c = Mathf.Cos((float)i * f);
            
            Vector2 v1 = new Vector2(-x + (1.0f - c) * bl * a1, -y + (1.0f - s) * bl * a2); // Bottom-Left
            Vector2 v2 = new Vector2(x - (1.0f - s) * br * a1,  -y + (1.0f - c) * br * a2); // Bottom-Right
            Vector2 v3 = new Vector2(x - (1.0f - c) * tr * a1,  y - (1.0f - s) * tr * a2);  // Top-Right
            Vector2 v4 = new Vector2(-x + (1.0f - s) * tl * a1, y - (1.0f - c) * tl * a2);  // Top-Left

            Vector2 p1 = (v1 * rs) + rectCenter;
            Vector2 p2 = (v2 * rs) + rectCenter;
            Vector2 p3 = (v3 * rs) + rectCenter;
            Vector2 p4 = (v4 * rs) + rectCenter;

            p1.y = Screen.height - p1.y;
            p2.y = Screen.height - p2.y;
            p3.y = Screen.height - p3.y;
            p4.y = Screen.height - p4.y;

            if (!usePercentage)
            {
                Vector2 adj = new Vector2(2.0f / rect.width, 2.0f / rect.height);
                v1 = new Vector2(v1.x * adj.x, v1.y * adj.y);
                v2 = new Vector2(v2.x * adj.x, v2.y * adj.y);
                v3 = new Vector2(v3.x * adj.x, v3.y * adj.y);
                v4 = new Vector2(v4.x * adj.x, v4.y * adj.y);
            }

            Vector2 uv1 = v1 * 0.5f + Vector2.one * 0.5f;
            Vector2 uv2 = v2 * 0.5f + Vector2.one * 0.5f;
            Vector2 uv3 = v3 * 0.5f + Vector2.one * 0.5f;
            Vector2 uv4 = v4 * 0.5f + Vector2.one * 0.5f;

            uv1.y = 1.0f - uv1.y;
            uv2.y = 1.0f - uv2.y;
            uv3.y = 1.0f - uv3.y;
            uv4.y = 1.0f - uv4.y;

            // Bottom-Left Sector
            int idx1 = perimeterStartVertex + i;
            vertices[idx1] = p1;
            uvs[idx1] = uv1;
            colors[idx1] = color.linear;

            // Bottom-Right Sector
            int idx2 = perimeterStartVertex + cornerVertexCount + i;
            vertices[idx2] = p2;
            uvs[idx2] = uv2;
            colors[idx2] = color.linear;

            // Top-Right Sector
            int idx3 = perimeterStartVertex + (cornerVertexCount * 2) + i;
            vertices[idx3] = p3;
            uvs[idx3] = uv3;
            colors[idx3] = color.linear;

            // Top-Left Sector
            int idx4 = perimeterStartVertex + (cornerVertexCount * 3) + i;
            vertices[idx4] = p4;
            uvs[idx4] = uv4;
            colors[idx4] = color.linear;
        }

            int indexPtr = currentTriangleCount;
            for (int i = 0; i < count; i++)
            {
                int currentPerimeterIdx = perimeterStartVertex + i;
                int nextPerimeterIdx = perimeterStartVertex + ((i + 1) % count);

                triangles[indexPtr + 0] = centerVertexIndex;
                triangles[indexPtr + 1] = nextPerimeterIdx;
                triangles[indexPtr + 2] = currentPerimeterIdx;
                indexPtr += 3;
            }

            currentVertexCount += totalVertices;
            currentTriangleCount = indexPtr;

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                EnsureBatchCapacity(currentBatchCount + 1);

                batches[currentBatchCount] = new RenderBatch
                {
                    texture = texture == null ? defaultTexture : texture,
                    triangleStart = batchTriangleStart,
                    triangleCount = batchTriangleCount,
                    clippingRect = clippingRect
                };

                currentBatchCount++;
            }
        }

        public void DrawCircle(Vector2 center, float radius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            const int segments = 32;
            int totalVertices = segments + 1;
            int totalIndices = segments * 3;
            int batchTriangleStart = currentTriangleCount;

            EnsureListCapacity(currentVertexCount + totalVertices, currentTriangleCount + totalIndices);

            float centerX = center.x;
            float centerY = Screen.height - center.y;
            Color linearColor = color.linear;

            int centerVertexIndex = currentVertexCount;
            vertices[centerVertexIndex] = new Vector3(centerX, centerY, 0f);
            uvs[centerVertexIndex] = new Vector2(0.5f, 0.5f);
            colors[centerVertexIndex] = linearColor;

            int perimeterStartVertex = centerVertexIndex + 1;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i * (Mathf.PI * 2.0f) / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float xpos = centerX + cos * radius;
                float ypos = centerY + sin * radius;

                int vertIdx = perimeterStartVertex + i;
                vertices[vertIdx] = new Vector3(xpos, ypos, 0f);
                uvs[vertIdx] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                colors[vertIdx] = linearColor;
            }

            int indexPtr = currentTriangleCount;
            for (int i = 0; i < segments; i++)
            {
                int currentPerimeterIdx = perimeterStartVertex + i;
                int nextPerimeterIdx = perimeterStartVertex + ((i + 1) % segments);

                triangles[indexPtr + 0] = centerVertexIndex;
                triangles[indexPtr + 1] = nextPerimeterIdx;
                triangles[indexPtr + 2] = currentPerimeterIdx;
                indexPtr += 3;
            }

            currentVertexCount += totalVertices;
            currentTriangleCount += totalIndices;

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                EnsureBatchCapacity(currentBatchCount + 1);

                batches[currentBatchCount] = new RenderBatch
                {
                    texture = texture == null ? defaultTexture : texture,
                    triangleStart = batchTriangleStart,
                    triangleCount = batchTriangleCount,
                    clippingRect = clippingRect
                };

                currentBatchCount++;
            }
        }

        public void DrawLines(ReadOnlySpan<LineSegment> lines, Color color, float thickness = 1.0f, Rect clippingRect = default)
        {
            if (lines.Length == 0)
                return;

            int lineCount = lines.Length;
            int totalVertices = lineCount * 4;
            int totalIndices = lineCount * 6;
            int batchTriangleStart = currentTriangleCount;

            EnsureListCapacity(currentVertexCount + totalVertices, currentTriangleCount + totalIndices);

            float halfThickness = thickness * 0.5f;
            Color linearColor = color.linear;

            for (int i = 0; i < lineCount; i++)
            {
                LineSegment line = lines[i];

                // Convert coordinates from top-down space to native bottom-up screen space
                Vector2 start = new Vector2(line.from.x, Screen.height - line.from.y);
                Vector2 end = new Vector2(line.to.x, Screen.height - line.to.y);

                Vector2 direction = end - start;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    // Fallback direction if start and end positions are identical
                    direction = Vector2.right;
                }
                else
                {
                    direction.Normalize();
                }

                // Calculate the normal perpendicular vector to expand the line width
                Vector2 normal = new Vector2(-direction.y, direction.x) * halfThickness;

                // Quad corners mapping geometry positions
                Vector3 v0 = new Vector3(start.x + normal.x, start.y + normal.y, 0f);
                Vector3 v1 = new Vector3(start.x - normal.x, start.y - normal.y, 0f);
                Vector3 v2 = new Vector3(end.x - normal.x, end.y - normal.y, 0f);
                Vector3 v3 = new Vector3(end.x + normal.x, end.y + normal.y, 0f);

                int vertIdx = currentVertexCount + i * 4;

                vertices[vertIdx + 0] = v0;
                vertices[vertIdx + 1] = v1;
                vertices[vertIdx + 2] = v2;
                vertices[vertIdx + 3] = v3;

                // Populate standardized flat UV bounds for full texture coordinates compatibility
                uvs[vertIdx + 0] = new Vector2(0f, 0f);
                uvs[vertIdx + 1] = new Vector2(1f, 0f);
                uvs[vertIdx + 2] = new Vector2(1f, 1f);
                uvs[vertIdx + 3] = new Vector2(0f, 1f);

                colors[vertIdx + 0] = linearColor;
                colors[vertIdx + 1] = linearColor;
                colors[vertIdx + 2] = linearColor;
                colors[vertIdx + 3] = linearColor;

                int triIdx = currentTriangleCount + i * 6;

                triangles[triIdx + 0] = vertIdx + 0;
                triangles[triIdx + 1] = vertIdx + 1;
                triangles[triIdx + 2] = vertIdx + 2;

                triangles[triIdx + 3] = vertIdx + 0;
                triangles[triIdx + 4] = vertIdx + 2;
                triangles[triIdx + 5] = vertIdx + 3;
            }

            currentVertexCount += totalVertices;
            currentTriangleCount += totalIndices;

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                EnsureBatchCapacity(currentBatchCount + 1);

                batches[currentBatchCount] = new RenderBatch
                {
                    texture = defaultTexture,
                    triangleStart = batchTriangleStart,
                    triangleCount = batchTriangleCount,
                    clippingRect = clippingRect
                };

                currentBatchCount++;
            }
        }

        private void EnsureListCapacity(int requiredVertices, int requiredTriangles)
        {
            // Pad the underlying storage arrays if our rendering loop needs more room
            while (vertices.Count < requiredVertices)
            {
                vertices.Add(Vector3.zero);
                uvs.Add(Vector2.zero);
                colors.Add(Color.white);
            }

            while (triangles.Count < requiredTriangles)
            {
                triangles.Add(0);
            }
        }

        private void EnsureBatchCapacity(int requiredBatches)
        {
            // Pad the underlying storage arrays if our rendering loop needs more room
            while (batches.Count < requiredBatches)
            {
                batches.Add(default);
            }
        }

        private void EnsureSubMeshCapacity(int requiredSubMeshes)
        {
            // Pad the underlying storage arrays if our rendering loop needs more room
            while (subMeshDescriptors.Count < requiredSubMeshes)
            {
                subMeshDescriptors.Add(default);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LineSegment
    {
        public Vector2 from;
        public Vector2 to;
    }
}