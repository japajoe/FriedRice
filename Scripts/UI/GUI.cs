using UnityEngine;
using UnityEngine.InputSystem;
using FriedRice.Core;
using System;
using System.Collections.Generic;

namespace FriedRice.UI
{
    using Font = FriedRice.Core.Font;

    public interface IControlEventController
    {
        void SetHovered(bool hovered);
        void SetFocused(bool focused);
        void SetMouseDown(bool down);
        void SetMouseUp(bool up);
        void SetMousePressed(bool pressed);
        void StoreState();
    }

    public abstract class Control : IControlEventController
    {
        protected enum ControlState : int
        {
            None = 1 << 0,
            Hovered = 1 << 1,
            Active = 1 << 2,
            Focused = 1 << 3,
        }

        protected enum MouseButtonState
        {
            None = 1 << 0,
            Down = 1 << 1,
            Up = 1 << 2,
            Pressed = 1 << 3,
        }

        protected struct State
        {
            public ControlState controlState;
            public MouseButtonState mouseButtonState;
        }

        protected State state;
        protected State previousState;
        protected Control parent;
        protected Vector2 position;
        protected Vector2 size;
        protected static Font font;
        private int id;

        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        public Vector2 Size
        {
            get => size;
            set => size = value;
        }

        public Control Parent => parent;

        public int Id => id;

        public Control()
        {
            id = GetHashCode();
        }

        public virtual void OnPaint() {}
        
        public void SetParent(Control parent)
        {
            this.parent = parent;
        }

        void IControlEventController.SetHovered(bool hovered)
        {
            if(hovered)
                state.controlState |= ControlState.Hovered;
            else
                state.controlState &= ~ControlState.Hovered;
        }

        void IControlEventController.SetFocused(bool focused)
        {
            if(focused)
                state.controlState |= ControlState.Focused;
            else
                state.controlState &= ~ControlState.Focused;
        }

        void IControlEventController.SetMouseDown(bool down)
        {
            if(down)
                state.mouseButtonState |= MouseButtonState.Down;
            else
                state.mouseButtonState &= ~MouseButtonState.Down;
        }

        void IControlEventController.SetMouseUp(bool up)
        {
            if(up)
                state.mouseButtonState |= MouseButtonState.Up;
            else
                state.mouseButtonState &= ~MouseButtonState.Up;
        }

        void IControlEventController.SetMousePressed(bool pressed)
        {
            if(pressed)
                state.mouseButtonState |= MouseButtonState.Pressed;
            else
                state.mouseButtonState &= ~MouseButtonState.Pressed;
        }

        void IControlEventController.StoreState()
        {
            previousState = state;
        }

        protected static bool IsMouseDown()
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }

        protected static bool IsMouseUp()
        {
            return Mouse.current.leftButton.wasReleasedThisFrame;
        }

        protected static bool IsMousePressed()
        {
            return Mouse.current.leftButton.isPressed;
        }

        protected static Vector2 GetMousePosition()
        {
            Vector2 position = Mouse.current.position.ReadValue();
            position.y = Screen.height - position.y;
            return position;
        }

        protected static bool IsMouseOnRect(Rect rect)
        {
            return rect.Contains(GetMousePosition());
        }

        protected static Vector2 GetTextBounds(ReadOnlySpan<char> text, float fontSize)
        {
            Vector2 bounds = new Vector2();
            font.CalculateBounds(text, fontSize, out bounds.x, out bounds.y);
            return bounds;
        }

        protected static Vector2 GetTextCenterPosition(Rect targetArea, ReadOnlySpan<char> text, float fontSize)
        {
            Vector2 textSize = GetTextBounds(text, fontSize);
            float x = targetArea.x + (targetArea.width - textSize.x) * 0.5f;
            float y = targetArea.y + (targetArea.height - textSize.y) * 0.5f;
            return new Vector2(x, y);
        }

        protected static Rect CreateRect(Vector2 position, Vector2 size)
        {
            return new Rect(position.x, position.y, size.x, size.y);
        }

        protected static Vector2 GetPositionRelativeToParent(Control control)
        {
            if(control == null)
                return Vector2.zero;
            
            Control parent = control.Parent;

            if(parent == null)
                return control.Position;

            Vector2 position = control.Position;

            while(parent != null)
            {
                position += parent.Position;
                parent = parent.Parent;
            }
            return position;
        }
    }

    public sealed class Surface : Control, IDisposable
    {
        private List<Control> children;

        public Surface(string fontFilePath) : base()
        {
            children = new List<Control>();
            if(font == null)
            {
                font = new Font();
                font.Generate(fontFilePath, 16, FontRenderMethod.Normal, true);
            }
        }

        public void Add(Control child)
        {
            child.SetParent(this);
            children.Add(child);
        }

        public void Update()
        {
            OnUpdate();
            OnPaint();
        }

        private void OnUpdate()
        {
            position = new Vector2(0, 0);
            size = new Vector2(Screen.width, Screen.height);

            for(int i = 0; i < children.Count; i++)
            {
                Vector2 position = GetPositionRelativeToParent(children[i]);
                Vector2 size = children[i].Size;
                Rect rect = new Rect(position.x, position.y, size.x, size.y);
                IControlEventController controller = (IControlEventController)children[i];

                controller.StoreState();

                if(IsMouseDown())
                    controller.SetMouseDown(true);
                else
                    controller.SetMouseDown(false);

                if(IsMouseUp())
                    controller.SetMouseUp(true);
                else
                    controller.SetMouseUp(false);

                if(IsMousePressed())
                    controller.SetMousePressed(true);
                else
                    controller.SetMousePressed(false);

                if(IsMouseOnRect(rect))
                    controller.SetHovered(true);
                else
                    controller.SetHovered(false);
            }
        }

        public override void OnPaint()
        {
            for(int i = 0; i < children.Count; i++)
                children[i].OnPaint();
        }

        public void Dispose()
        {
            font.Dispose();
        }
    }
    
    public class Button : Control
    {
        private string text;
        private float fontSize;
        private Color color = Color.black;
        private Color colorHovered = Color.grey;
        private Color colorActive = Color.darkGray;
        private Color colorText = Color.white;

        public event EventHandler Clicked;
        
        public string Text
        {
            get => text;
            set => text = value;
        }

        public Button() : base()
        {
            text = "Button";
            fontSize = 16;
            position = Vector2.zero;
            size = new Vector2(80, 20);
        }

        public override void OnPaint()
        {
            Vector2 position = GetPositionRelativeToParent(this);
            ReadOnlySpan<char> pText = text.AsSpan();
            Rect rect = CreateRect(position, size);
            Vector2 textPosition = GetTextCenterPosition(rect, pText, fontSize);
            Color buttonColor = color;

            bool isHovered = (state.controlState & ControlState.Hovered) == ControlState.Hovered;
            bool isPressed = state.mouseButtonState == MouseButtonState.Pressed;
            bool isReleased = state.mouseButtonState == MouseButtonState.Up;

            if(isHovered)
            {
                if(isPressed)
                    buttonColor = colorActive;
                else
                    buttonColor = colorHovered;
            }

            Graphics2D.DrawRectangleEx(rect, 2.0f, buttonColor);
            Graphics2D.DrawText(font, text, textPosition, fontSize, colorText);

            if(isHovered)
            {
                if(isReleased)    
                    Clicked?.Invoke(this, null);
            }
        }
    }
}