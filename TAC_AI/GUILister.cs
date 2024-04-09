using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TAC_AI
{
    public class GUIListHandler
    {
        public virtual string GetName()
        {
            return "Unset";
        }
        public virtual int GetID()
        {
            return 1321344;
        }
        internal virtual void GUIHandler(int ID)
        {
        }
        public Rect HotWindow = new Rect(0, 0, 200, 230);   // the "window"
    }
    public class GUIListHandler<T> : GUIListHandler
    {
        private class GUIDisplayListHandler : MonoBehaviour
        {
            internal GUIListHandler handler;
            private void OnGUI()
            {
                if (enabled && KickStart.CanUseMenu)
                {
                    handler.HotWindow = GUI.Window(handler.GetID(), handler.HotWindow, handler.GUIHandler, "<b>Debug Prefab Spawns</b>");
                }
            }
        }
        public static GUIListHandler<T> Initiate(List<T> list)
        {
            DebugTAC_AI.Log(KickStart.ModID + ": GUIListHandler for " + typeof(T).Name);

            GUIListHandler<T> GLH = new GUIListHandler<T>();
            GLH.GUIWindow = new GameObject(GLH.GetName());
            var DLH = GLH.GUIWindow.AddComponent<GUIDisplayListHandler>();
            DLH.handler = GLH;
            GLH.GUIWindow.SetActive(false);
            GLH.ListElements = list;
            return GLH;
        }
        public void ReInit(List<T> list)
        {
            if (!GUIWindow)
                return;
            ListElements = list;
        }
        public void DeInit()
        {
            if (!GUIWindow)
                return;
            UnityEngine.Object.Destroy(GUIWindow);
            GUIWindow = null;
        }
        private GUIListHandler(){}

        private GameObject GUIWindow;

        protected int ButtonWidth = 200;
        protected int MaxCountWidth = 4;
        protected int MaxWindowHeight = 500;
        private int MaxWindowWidth => MaxCountWidth * ButtonWidth;

        private List<T> ListElements = new List<T>();
        private float scrolllSize = 50;
        private Vector2 scrolll = new Vector2(0, 0);
        private int VertPosOff = 0;
        private int HoriPosOff = 0;
        private bool MaxExtensionX = false;
        private bool MaxExtensionY = false;
        protected virtual void GUIHandlerPre(int ID)
        { 
        }

        protected virtual bool GUIButtonHandler(T ButtonContext)
        {
            GUISpacerAuto();
            if (GUI.Button(new Rect(20 + HoriPosOff, VertPosOff, ButtonWidth, 30), ButtonContext.ToString()))
            {
                return true;
            }
            return false;
        }
        protected virtual void GUIButtonHandlerPostEvent(T ButtonContext)
        {
        }

        internal override void GUIHandler(int ID)
        {
            VertPosOff = 0;
            HoriPosOff = 0;
            MaxExtensionX = false;
            MaxExtensionY = false;
            int index = -1;

            scrolll = GUI.BeginScrollView(new Rect(0, 30, HotWindow.width - 20, HotWindow.height - 40), scrolll, new Rect(0, 0, HotWindow.width - 50, scrolllSize));
            GUIHandlerPre(ID);
            if (ListElements == null || ListElements.Count() == 0)
            {
                if (GUI.Button(new Rect(20 + HoriPosOff, 30 + VertPosOff, ButtonWidth, 30), "Nothing"))
                {
                }
                return;
            }
            else
            {
                int Entries = ListElements.Count();
                for (int step = 0; step < Entries; step++)
                {
                    try
                    {
                        if (GUIButtonHandler(ListElements[step]))
                        {
                            index = step;
                        }
                        HoriPosOff += ButtonWidth;
                    }
                    catch { }// error on handling something
                }
            }

            GUI.EndScrollView();
            scrolllSize = VertPosOff + 80;

            if (MaxExtensionY)
                HotWindow.height = MaxWindowHeight + 80;
            else
                HotWindow.height = VertPosOff + 80;

            if (MaxExtensionX)
                HotWindow.width = MaxWindowWidth + 60;
            else
                HotWindow.width = HoriPosOff + 60;
            if (index != -1)
            {
                GUIButtonHandlerPostEvent(ListElements[index]);
            }

            GUI.DragWindow();
        }


        protected void GUISpacerAuto()
        {
            if (HoriPosOff >= MaxWindowWidth)
            {
                HoriPosOff = 0;
                VertPosOff += 30;
                MaxExtensionX = true;
                if (VertPosOff >= MaxWindowHeight)
                    MaxExtensionY = true;
            }
        }

        public void LaunchSubMenuClickable()
        {
            if (!GUIWindow.activeSelf)
            {
                DebugTAC_AI.Log(KickStart.ModID + ": Opened " + typeof(T).Name + " menu!");
                OnLaunchSubMenu();
                GUIWindow.SetActive(true);
            }
        }
        protected virtual void OnLaunchSubMenu()
        {
        }
        public void CloseSubMenuClickable()
        {
            if (GUIWindow.activeSelf)
            {
                OnCloseSubMenu();
                GUIWindow.SetActive(false);
                KickStart.ReleaseControl();
                DebugTAC_AI.Log(KickStart.ModID + ": Closed " + typeof(T).Name + " menu!");
            }
        }
        protected virtual void OnCloseSubMenu()
        {
        }
    }
}
