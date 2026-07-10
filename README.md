# FriedRice
Immediate mode rendering of shapes and texts, without having to create a gazillion of game objects. This is an experimental library.

```cs
using System.IO;
using UnityEngine;
using FriedRice.Core;

namespace FriedRice
{
    using Font = FriedRice.Core.Font;
    
    public class GraphicsTester : MonoBehaviour
    {
        private Font font1;
        private Font font2;

        [Range(1.0f, 128.0f)]
        public float radius = 5.0f;

        private LineSegment[] lines;

        private void Start()
        {
            font1 = new Font();
            font2 = new Font();

            string filePath = Path.Combine(Application.dataPath, "Path/To/Font1.ttf");
            if (!font1.Generate(filePath, 64, FontRenderMethod.Normal, true))
            {
                Debug.LogError("Failed to load Font1");
            }

            filePath = Path.Combine(Application.dataPath, "Path/To/Font2.ttf");
            if (!font2.Generate(filePath, 64, FontRenderMethod.Normal, true))
            {
                Debug.LogError("Failed to load Font2");
            }

            Rect bounds = new Rect(10, 210, 500, 200);
            
            lines = new LineSegment[50];

            int sampleCount = 100;
            int lineCount = sampleCount - 1; // 99 connecting lines for 100 samples
            lines = new LineSegment[lineCount];
            Vector2[] samples = new Vector2[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float x = bounds.x + t * bounds.width;
                
                float sinVal = (Mathf.Sin(t * Mathf.PI * 2f) * 0.5f) + 0.5f;
                float y = bounds.y + sinVal * bounds.height;

                samples[i] = new Vector2(x, y);
            }

            for (int i = 0; i < lineCount; i++)
            {
                lines[i] = new LineSegment
                {
                    from = samples[i],
                    to = samples[i + 1]
                };
            }
        }

        private void OnApplicationQuit()
        {
            font1.Dispose();
            font2.Dispose();
        }

        private void Update()
        {
            Graphics2D.DrawRectangle(new Rect(10, 10, 500, 200), Color.white);
            Graphics2D.DrawRectangleEx(new Rect(10, 210, 500, 200), radius, Color.darkGray);

            Graphics2D.DrawText(font1, "Hello world!", new Vector2(10, 10), 32, Color.black);
            Graphics2D.DrawText(font1, "Привет, мир!", new Vector2(10, 60), 32, Color.purple);
            Graphics2D.DrawText(font2, "This is a different font", new Vector2(10, 110), 32, Color.red);
            Graphics2D.DrawText(font2, "Test", new Vector2(10, 160), 32, Color.green);

            Graphics2D.DrawLines(lines, Color.navyBlue, 2.0f);
        }
    }
}
```