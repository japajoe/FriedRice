using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;

namespace FriedRice.Core
{
    public static class Graphics2D
    {
        private static ShapeRenderer shapeRenderer = null;
        private static TextRenderer textRenderer = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            shapeRenderer = new ShapeRenderer();
            textRenderer = new TextRenderer();

            PlayerLoopSystem currentLoop = PlayerLoop.GetCurrentPlayerLoop();

            InsertNewFrameCallback(ref currentLoop);
            InsertEndFrameCallback(ref currentLoop);

            PlayerLoop.SetPlayerLoop(currentLoop);
            Application.quitting -= Destroy;
            Application.quitting += Destroy;

            RenderPipelineManager.endCameraRendering += OnRender;
        }

        private static void Destroy()
        {
            RenderPipelineManager.endCameraRendering -= OnRender;

            PlayerLoopSystem currentLoop = PlayerLoop.GetCurrentPlayerLoop();

            RemoveNewFrameCallback(ref currentLoop);
            RemoveEndFrameCallback(ref currentLoop);

            PlayerLoop.SetPlayerLoop(currentLoop);

            shapeRenderer.Dispose();
            textRenderer.Dispose();
        }

        public static void NewFrame()
        {
            shapeRenderer.NewFrame();
            textRenderer.NewFrame();
        }

        public static void EndFrame()
        {
            shapeRenderer.EndFrame();
            textRenderer.EndFrame();
        }

        public static void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, float angleDegrees, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            shapeRenderer.DrawTriangle(p1, p2, p3, angleDegrees, color, texture, clippingRect);
        }

        public static void DrawRectangle(Rect rect, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            shapeRenderer.DrawRectangle(rect, color, texture, clippingRect);
        }

        public static void DrawRectangleEx(Rect rect, float radius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            shapeRenderer.DrawRectangleEx(rect, radius, color, texture, clippingRect);
        }

        public static void DrawRectangleEx(Rect rect, float radius, float bottomLeftRadius, float bottomRightRadius, float topLeftRadius, float topRightRadius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            shapeRenderer.DrawRectangleEx(rect, radius, bottomLeftRadius, bottomRightRadius, topLeftRadius, topRightRadius, color, texture, clippingRect);
        }
        
        public static void DrawCircle(Vector2 center, float radius, Color color, Texture2D texture = null, Rect clippingRect = default)
        {
            shapeRenderer.DrawCircle(center, radius, color, texture, clippingRect);
        }

        public static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness = 1.0f, Rect clippingRect = default)
        {
            Span<LineSegment> lines = stackalloc LineSegment[1];
            LineSegment line = new LineSegment();
            line.from = from;
            line.to = to;
            lines[0] = line;
            shapeRenderer.DrawLines(lines, color, thickness, clippingRect);
        }

        public static void DrawLines(ReadOnlySpan<LineSegment> lines, Color color, float thickness = 1.0f, Rect clippingRect = default)
        {
            shapeRenderer.DrawLines(lines, color, thickness, clippingRect);
        }

        public static void DrawText(Font font, string text, Vector2 position, float fontSize, Color color)
        {
            textRenderer.DrawText(font, text, position, fontSize, color);
        }

        public static void DrawText(Font font, ReadOnlySpan<char> text, Vector2 position, float fontSize, Color color, Rect clippingRect = default)
        {
            textRenderer.DrawText(font, text, position, fontSize, color, clippingRect);
        }

        private static void OnRender(ScriptableRenderContext context, Camera camera)
        {
            // Prevent UI rendering inside the scene view or preview cameras
            if (camera.cameraType != CameraType.Game)
                return;

            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(0, Screen.width, 0, Screen.height, -1, 1);

            GL.PushMatrix();
            GL.LoadIdentity();
            GL.LoadProjectionMatrix(projectionMatrix);

            shapeRenderer.Draw();
            textRenderer.Draw();

            GL.PopMatrix();
        }

        private static void InsertNewFrameCallback(ref PlayerLoopSystem rootLoop)
        {
            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                if (rootLoop.subSystemList[i].type == typeof(PreUpdate))
                {
                    PlayerLoopSystem preUpdateSystem = rootLoop.subSystemList[i];

                    if(preUpdateSystem.subSystemList == null)
                    {
                        preUpdateSystem.subSystemList = new PlayerLoopSystem[0];
                    }

                    List<PlayerLoopSystem> subSystems = new List<PlayerLoopSystem>(preUpdateSystem.subSystemList);

                    PlayerLoopSystem newFrameSystem = new PlayerLoopSystem
                    {
                        type = typeof(Graphics2DNewFrameMarker),
                        updateDelegate = Graphics2D.NewFrame
                    };

                    // Add to the end of PreUpdate so it runs right before Update starts
                    subSystems.Add(newFrameSystem);

                    preUpdateSystem.subSystemList = subSystems.ToArray();
                    rootLoop.subSystemList[i] = preUpdateSystem;
                    break;
                }
            }
        }

        private static void InsertEndFrameCallback(ref PlayerLoopSystem rootLoop)
        {
            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                if (rootLoop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    PlayerLoopSystem postLateUpdateSystem = rootLoop.subSystemList[i];

                    if(postLateUpdateSystem.subSystemList == null)
                    {
                        postLateUpdateSystem.subSystemList = new PlayerLoopSystem[0];
                    }

                    List<PlayerLoopSystem> subSystems = new List<PlayerLoopSystem>(postLateUpdateSystem.subSystemList);

                    PlayerLoopSystem endFrameSystem = new PlayerLoopSystem
                    {
                        type = typeof(Graphics2DEndFrameMarker),
                        updateDelegate = Graphics2D.EndFrame
                    };

                    // Insert at the beginning of PostLateUpdate so it runs right after LateUpdate finishes
                    subSystems.Insert(0, endFrameSystem);

                    postLateUpdateSystem.subSystemList = subSystems.ToArray();
                    rootLoop.subSystemList[i] = postLateUpdateSystem;
                    break;
                }
            }
        }

        private static void RemoveNewFrameCallback(ref PlayerLoopSystem rootLoop)
        {
            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                if (rootLoop.subSystemList[i].type == typeof(PreUpdate))
                {
                    PlayerLoopSystem preUpdateSystem = rootLoop.subSystemList[i];

                    if(preUpdateSystem.subSystemList == null)
                    {
                        preUpdateSystem.subSystemList = new PlayerLoopSystem[0];
                    }

                    List<PlayerLoopSystem> subSystems = new List<PlayerLoopSystem>(preUpdateSystem.subSystemList);

                    for (int j = subSystems.Count - 1; j >= 0; j--)
                    {
                        if (subSystems[j].type == typeof(Graphics2DNewFrameMarker))
                        {
                            subSystems.RemoveAt(j);
                        }
                    }

                    preUpdateSystem.subSystemList = subSystems.ToArray();
                    rootLoop.subSystemList[i] = preUpdateSystem;
                    break;
                }
            }
        }

        private static void RemoveEndFrameCallback(ref PlayerLoopSystem rootLoop)
        {
            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                if (rootLoop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    PlayerLoopSystem postLateUpdateSystem = rootLoop.subSystemList[i];

                    if(postLateUpdateSystem.subSystemList == null)
                    {
                        postLateUpdateSystem.subSystemList = new PlayerLoopSystem[0];
                    }

                    List<PlayerLoopSystem> subSystems = new List<PlayerLoopSystem>(postLateUpdateSystem.subSystemList);

                    for (int j = subSystems.Count - 1; j >= 0; j--)
                    {
                        if (subSystems[j].type == typeof(Graphics2DEndFrameMarker))
                        {
                            subSystems.RemoveAt(j);
                        }
                    }

                    postLateUpdateSystem.subSystemList = subSystems.ToArray();
                    rootLoop.subSystemList[i] = postLateUpdateSystem;
                    break;
                }
            }
        }

        // Custom structural markers required by the PlayerLoop API system layout
        private struct Graphics2DNewFrameMarker { }
        private struct Graphics2DEndFrameMarker { }
    }

    public interface IRenderer
    {
        void NewFrame();
        void EndFrame();
        void Draw();
        void Dispose();
    }

    public struct RenderBatch
    {
        public Texture2D texture;
        public Rect clippingRect;
        public bool SDF;
        public int triangleStart;
        public int triangleCount;
    }
}