using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FriedRice.Core
{
    public sealed class TextRenderer : IRenderer, IDisposable
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

        public TextRenderer()
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

            material = new Material(Shader.Find("FriedRice/FontShader"));
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
            else
            {
                mesh.Clear();
            }
        }

        public void Draw()
        {
            if (mesh == null || mesh.vertexCount == 0 || material == null || currentBatchCount == 0)
                return;

            int mainTexId = Shader.PropertyToID("_MainTex");
            int clipRectId = Shader.PropertyToID("_ClipRect");
            int screenSizeId = Shader.PropertyToID("_ScreenSize");

            Texture2D lastTexture = null;
            Vector4 screenSize = new Vector4(Screen.width, Screen.height, 0, 0);

            for (int i = 0; i < currentBatchCount; i++)
            {
                RenderBatch batch = batches[i];

                if(lastTexture != batch.texture)
                {
                    lastTexture = batch.texture;
                    material.SetTexture(mainTexId, batch.texture);
                }
                
                Vector4 clippingRect = new Vector4(batch.clippingRect.x, batch.clippingRect.y, batch.clippingRect.width, batch.clippingRect.height);
                material.SetVector(clipRectId, clippingRect);
                material.SetVector(screenSizeId, screenSize);
                
                material.SetPass(0);

                UnityEngine.Graphics.DrawMeshNow(mesh, Matrix4x4.identity, i);
            }
        }

        public void DrawText(Font font, string text, Vector2 position, float fontSize, Color color, Rect clippingRect = default)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var span = text.AsSpan();
            DrawText(font, span, position, fontSize, color, clippingRect);
        }

        public void DrawText(Font font, ReadOnlySpan<char> text, Vector2 position, float fontSize, Color color, Rect clippingRect = default)
        {
            if (text.Length == 0 || font == null)
                return;

            if(!font.IsLoaded)
                return;

            position.y = Screen.height - position.y;

            Vector2 pos = new Vector2(position.x, position.y);
            pos.y -= font.CalculateYOffset(fontSize);

            float originX = pos.x;
            float scale = font.GetPixelScale(fontSize);

            int batchTriangleStart = currentTriangleCount;

            UInt32 codePoint = Font.GetCodePoint(text, 0, out UInt32 codePointSize);

            if(codePointSize > 0)
            {
                var firstGlyph = font.GetGlyph(codePoint);

                if(firstGlyph != null)
                {
                    pos.x -= firstGlyph.bearingX * scale;
                }
            }

            for (int i = 0; i < text.Length; i++)
            {
                codePoint = Font.GetCodePoint(text, i, out codePointSize);

                if (codePoint == 10)
                {
                    pos.x = originX;
                    pos.y -= font.GetMaxHeight() * scale;
                    continue;
                }

                if (codePointSize == 0)
                {
                    continue;
                }

                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }

                Glyph glyph = font.GetGlyph(codePoint);

                if (glyph == null)
                    continue;

                float xpos = pos.x + glyph.bearingX * scale;
                float ypos = pos.y - (glyph.height - glyph.bearingY) * scale;
                float w = glyph.width * scale;
                float h = glyph.height * scale;

                // Ensure internal list capacities are expanded before index mapping access
                EnsureListCapacity(currentVertexCount + 4, currentTriangleCount + 6);

                // Overwrite the values inside the data pool directly via index tracking pointers
                vertices[currentVertexCount + 0] = new Vector3(xpos + w, ypos + h, 0);
                vertices[currentVertexCount + 1] = new Vector3(xpos, ypos + h, 0);
                vertices[currentVertexCount + 2] = new Vector3(xpos, ypos, 0);
                vertices[currentVertexCount + 3] = new Vector3(xpos + w, ypos, 0);

                uvs[currentVertexCount + 0] = new Vector2(glyph.u1, glyph.v0);
                uvs[currentVertexCount + 1] = new Vector2(glyph.u0, glyph.v0);
                uvs[currentVertexCount + 2] = new Vector2(glyph.u0, glyph.v1);
                uvs[currentVertexCount + 3] = new Vector2(glyph.u1, glyph.v1);

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

                pos.x += glyph.advanceX * scale;
            }

            int batchTriangleCount = currentTriangleCount - batchTriangleStart;

            if (batchTriangleCount > 0)
            {
                // Ensure the batch pool has internal list elements allocated
                EnsureBatchCapacity(currentBatchCount + 1);

                // FIX: Overwrite directly via structural index tracking instead of calling .Add()
                batches[currentBatchCount] = new RenderBatch
                {
                    texture = font.Texture,
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
}