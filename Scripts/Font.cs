using System;
using System.Collections.Generic;
using UnityEngine;
using FreeTypeSharp;
using Unity.Collections.LowLevel.Unsafe;

using static FreeTypeSharp.FT;

namespace FriedRice.Core
{
    public sealed unsafe class Font : IDisposable
    {
        private Texture2D texture;
        private UInt32 pixelSize;
        private Int32 shelf_x;
        private Int32 shelf_y;
        private Int32 shelf_h;
        private List<Glyph> glyphs;
        private FT_LibraryRec_* ft;
        private FT_FaceRec_* face;
        private UInt32 maxHeight;
        private UInt32 lineHeight;
        private FontRenderMethod renderMethod;
        private bool isLoaded;
        private const Int32 ATLAS_SIZE = 2048;
        private const int MAX_GLYPHS = UInt16.MaxValue;

        public Texture2D Texture => texture;
        public bool IsLoaded => isLoaded;

        public Font()
        {
            texture = null;
            pixelSize = 32;
            shelf_x = 1;
            shelf_y = 1;
            shelf_h = 0;
            glyphs = new List<Glyph>();
            ft = null;
            face = null;
            maxHeight = 0;
            lineHeight = 0;
            renderMethod = FontRenderMethod.Normal;
            isLoaded = false;
        }

        public bool Generate(string filePath, Int32 pixelSize, FontRenderMethod renderMethod, bool generateTexture = true)
        {
            if (texture != null)
                return true;

            this.pixelSize = (UInt32)pixelSize;
            this.renderMethod = renderMethod;

            FT_LibraryRec_* pFt = null;

            if (FT_Init_FreeType(&pFt) != FT_Error.FT_Err_Ok)
            {
                ft = null;
                face = null;
                Debug.LogError("FT init failed");
                return false;
            }

            ft = pFt;

            FT_FaceRec_* pFace = null;

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(filePath + "\0"); // null-terminate

            fixed (byte* pFilePath = &bytes[0])
            {
                if (FT_New_Face(ft, pFilePath, IntPtr.Zero, &pFace) != FT_Error.FT_Err_Ok)
                {
                    FT_Done_FreeType(ft);
                    ft = null;
                    face = null;
                    Debug.LogError($"Could not load font {filePath}");
                    return false;
                }
            }

            face = pFace;

            FT_Set_Pixel_Sizes(face, 0, (UInt32)pixelSize);

            maxHeight = (UInt32)face->size->metrics.height >> 6;
            lineHeight = (UInt32)face->size->metrics.height >> 6;

            LoadGlyphs();

            if (generateTexture)
                return GenerateTexture();

            return true;
        }

        public bool Generate(byte[] fontData, Int32 pixelSize, FontRenderMethod renderMethod, bool generateTexture = true)
        {
            if (texture != null)
                return true;

            this.pixelSize = (UInt32)pixelSize;
            this.renderMethod = renderMethod;

            FT_LibraryRec_* pFt = null;

            if (FT_Init_FreeType(&pFt) != FT_Error.FT_Err_Ok)
            {
                ft = null;
                face = null;
                Debug.LogError("FT init failed");
                return false;
            }

            ft = pFt;

            FT_FaceRec_* pFace = null;

            fixed (byte* pFileData = &fontData[0])
            {
                if (FT_New_Memory_Face(ft, pFileData, new IntPtr(fontData.Length), IntPtr.Zero, &pFace) != FT_Error.FT_Err_Ok)
                {
                    FT_Done_FreeType(ft);
                    ft = null;
                    face = null;
                    Debug.LogError($"Could not load font");
                    return false;
                }
            }

            face = pFace;

            FT_Set_Pixel_Sizes(face, 0, (UInt32)pixelSize);

            maxHeight = (UInt32)face->size->metrics.height >> 6;
            lineHeight = (UInt32)face->size->metrics.height >> 6;

            LoadGlyphs();

            if (generateTexture)
                return GenerateTexture();

            return true;
        }

        public void Dispose()
        {
            FT_Done_Face(face);
            FT_Done_FreeType(ft);
            face = null;
            ft = null;
            isLoaded = false;
        }

        public Glyph GetGlyph(UInt32 cp)
        {
            if (cp >= MAX_GLYPHS)
                return null;

            Glyph g = glyphs[(int)cp];

            if (g.loaded > 0)
                return g;

            UInt32 glyph_index = FT_Get_Char_Index(face, new UIntPtr(cp));

            if (glyph_index == 0)
                return null;

            if (FT_Load_Glyph(face, glyph_index, FT_LOAD.FT_LOAD_RENDER) != FT_Error.FT_Err_Ok)
                return null;

            FT_Render_Mode_ mode = renderMethod == FontRenderMethod.Normal ? FT_Render_Mode_.FT_RENDER_MODE_NORMAL : FT_Render_Mode_.FT_RENDER_MODE_SDF;

            if (FT_Render_Glyph(face->glyph, mode) != FT_Error.FT_Err_Ok)
                return null;

            if (!UploadGlyph(face->glyph, g))
                return null;

            g.codepoint = cp;
            return g;
        }

        public float GetPixelScale(float fontSize)
        {
            return fontSize / pixelSize;
        }

        public UInt32 GetMaxHeight()
        {
            return maxHeight;
        }

        public UInt32 GetLineHeight()
        {
            return lineHeight;
        }

        public float CalculateYOffset(float fontSize)
        {
            float height = 0.0f;
            float yOffset = 0.0f;

            for (int i = 0; i < glyphs.Count; i++)
            {
                if (glyphs[i].codepoint == 10) //new line
                    continue;

                float h = glyphs[i].bearingY;

                if (h > height)
                {
                    height = h;
                    yOffset = (glyphs[i].bearingY - glyphs[i].bottomBearing) * GetPixelScale(fontSize);
                }
            }

            return yOffset;
        }

        public void CalculateBounds(string text, int size, float fontSize, out float width, out float height)
        {
            var s = text.AsSpan(0, size);
            CalculateBounds(s, fontSize, out width, out height);
        }

        public void CalculateBounds(ReadOnlySpan<char> text, float fontSize, out float width, out float height)
        {
            width = 0.0f;
            height = 0.0f;

            Int32 currentLineWidth = 0; // Width of the current line
            Int32 lineCount = 1;

            for (int i = 0; i < text.Length; i++)
            {
                UInt32 codePointSize = 0;
                UInt32 codepoint = GetCodePoint(text, i, out codePointSize);
                char c = text[i];

                if (codepoint == 0)
                {
                    continue;
                }

                if (c == '\n')
                {
                    // End of a line
                    if (currentLineWidth > width)
                    {
                        width = (float)currentLineWidth;
                    }
                    currentLineWidth = 0; // Reset for the next line
                    lineCount++; // Increment line count
                    continue;
                }

                Glyph pGlyph = GetGlyph(codepoint);

                if (pGlyph == null)
                    continue;

                // Accumulate the width using the advanceX of the glyph
                currentLineWidth += pGlyph.advanceX;
            }

            // Check the last line
            if (currentLineWidth > width)
            {
                width = (float)currentLineWidth;
            }

            if (lineCount > 1)
            {
                height = lineCount * GetMaxHeight();
            }
            else
            {
                height = GetMaxHeight();
            }

            width *= GetPixelScale(fontSize);
            height *= GetPixelScale(fontSize);
        }

        private void LoadGlyphs()
        {
            glyphs.Clear();

            for (int i = 0; i < MAX_GLYPHS; i++)
            {
                glyphs.Add(new Glyph());
            }

            UIntPtr charCode;
            UInt32 glyphIndex;

            charCode = FT_Get_First_Char(face, &glyphIndex);
            while (glyphIndex != 0)
            {
                if (FT_Load_Glyph(face, glyphIndex, FT_LOAD.FT_LOAD_DEFAULT) != FT_Error.FT_Err_Ok)
                {
                    UInt32 code = (UInt32)charCode;
                    Debug.LogError($"Failed to load glyph for charcode {code}");
                    charCode = FT_Get_Next_Char(face, charCode, &glyphIndex);
                    continue;
                }

                Glyph glyph = glyphs[(int)glyphIndex];

                glyph.width = (Int32)face->glyph->bitmap.width;
                glyph.height = (Int32)face->glyph->bitmap.rows;
                glyph.bearingX = face->glyph->bitmap_left;
                glyph.bearingY = face->glyph->bitmap_top;
                glyph.advanceX = (Int32)face->glyph->advance.x >> 6;
                glyph.advanceY = (Int32)face->glyph->advance.y >> 6;
                glyph.bottomBearing = (Int32)(face->glyph->bitmap.rows - face->glyph->bitmap_top);
                glyph.leftBearing = (Int32)(face->glyph->bitmap.width - face->glyph->bitmap_left);
                glyph.loaded = 0;

                charCode = FT_Get_Next_Char(face, charCode, &glyphIndex);
            }
        }

        private bool UploadGlyph(FT_GlyphSlotRec_* slot, Glyph g)
        {
            int w = (Int32)slot->bitmap.width;
            int h = (Int32)slot->bitmap.rows;

            if (!PackGlyph(w, h, out int x, out int y))
            {
                return false;
            }

            var pixelData = texture.GetRawTextureData<byte>();
            byte* texturePtr = (byte*)pixelData.GetUnsafePtr();

            // Perform the equivalent of GL_UNPACK_ALIGNMENT 1 row-by-row copy
            // accounts for the destination stride (atlasSize) vs source stride (w)
            for (int row = 0; row < h; row++)
            {
                int srcOffset = row * w;
                int destOffset = ((y + row) * ATLAS_SIZE) + x;

                UnsafeUtility.MemCpy(
                    texturePtr + destOffset,
                    slot->bitmap.buffer + srcOffset,
                    w
                );
            }

            texture.Apply(false);

            // // set glyph metrics and UVs
            g.width = w;
            g.height = h;
            g.bearingX = slot->bitmap_left;
            g.bearingY = slot->bitmap_top;
            g.advanceX = (Int32)slot->advance.x >> 6;
            g.advanceY = (Int32)slot->advance.y >> 6;
            g.bottomBearing = (Int32)(slot->bitmap.rows - slot->bitmap_top);
            g.leftBearing = (Int32)(slot->bitmap.width - slot->bitmap_left);

            g.u0 = (float)x / ATLAS_SIZE;
            g.v0 = (float)y / ATLAS_SIZE;
            g.u1 = (float)(x + w) / ATLAS_SIZE;
            g.v1 = (float)(y + h) / ATLAS_SIZE;

            g.loaded = 1;
            return true;
        }

        private bool GenerateTexture()
        {
            if (ft == null || face == null)
            {
                Debug.LogError("Could not generate texture because font face is not loaded");
                return false;
            }

            texture = new Texture2D(ATLAS_SIZE, ATLAS_SIZE, TextureFormat.R8, false);

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            // Clear the texture memory to clear out uninitialized garbage lines
            var pixelData = texture.GetRawTextureData<byte>();
            unsafe
            {
                byte* texturePtr = (byte*)pixelData.GetUnsafePtr();
                UnsafeUtility.MemClear(texturePtr, ATLAS_SIZE * ATLAS_SIZE);
            }

            texture.Apply(false, false);

            isLoaded = true;

            return true;
        }

        private bool PackGlyph(Int32 w, Int32 h, out Int32 outX, out Int32 outY)
        {
            outX = 0;
            outY = 0;

            if (w + 2 > ATLAS_SIZE || h + 2 > ATLAS_SIZE)
                return false;
            if (shelf_x + w + 1 > ATLAS_SIZE)
            {
                // new shelf
                shelf_x = 1;
                shelf_y += shelf_h + 1;
                shelf_h = 0;
            }
            if (shelf_y + h + 1 > ATLAS_SIZE)
                return false;
            outX = shelf_x;
            outY = shelf_y;
            shelf_x += w + 1;
            if (h > shelf_h)
                shelf_h = h;
            return true;
        }

        public static UInt32 GetCodePoint(ReadOnlySpan<char> s, int index, out UInt32 codePointSize)
        {
            if (index < 0 || index >= s.Length)
            {
                codePointSize = 0;
                return 0;
            }

            UInt32 val = (UInt32)s[index];

            if (char.IsHighSurrogate(s[index]) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
            {
                val = (UInt32)char.ConvertToUtf32(s[index], s[index + 1]);
            }

            if (val <= 0x7F)
            {
                codePointSize = 1;
                return val;
            }

            if (val <= 0x7FF)
            {
                codePointSize = 2;
                // FIX: Return the raw Unicode codepoint instead of the packed UTF-8 bytes
                return val;
            }

            if (val <= 0xFFFF)
            {
                codePointSize = 3;
                // FIX: Return the raw Unicode codepoint instead of the packed UTF-8 bytes
                return val;
            }

            codePointSize = 4;
            // FIX: Return the raw Unicode codepoint instead of the packed UTF-8 bytes
            return val;
        }
    }

    public class Glyph
    {
        public UInt32 codepoint;
        public Int32 width, height;
        public Int32 bearingX, bearingY;
        public Int32 advanceX, advanceY;
        public Int32 bottomBearing, leftBearing;
        public float u0, v0, u1, v1; // UVs in atlas
        public Int32 loaded;
    }

    public enum FontRenderMethod
    {
        Normal,
        SDF
    }
}