using System;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal sealed class ResearchReportWindow
    {
        private const int WindowId = 0x50435257;
        private readonly ResearchWindowModel model = new ResearchWindowModel();
        private readonly GUI.WindowFunction drawContents;
        private Rect rect;
        private Vector2 listScroll, reportScroll;
        private GUIStyle? text;
        private ResearchWindowView? view;

        internal ResearchReportWindow() { drawContents = DrawContents; }

        internal bool Open => model.Open;
        internal long Builds => model.Builds;
        internal void Show() => model.Show();
        internal void Hide() { model.Hide(); view = null; }
        internal void Toggle() { model.Toggle(); if (!model.Open) view = null; }

        internal void Draw(ResearchLedger? ledger, Func<string, string> businessName)
        {
            if (!model.Open) return;
            view = model.Update(Time.unscaledTime, ledger, businessName);
            if (view == null) return;
            if (rect.width <= 0) rect = new Rect(Math.Max(20, Screen.width - 700), 20, Math.Min(680, Screen.width - 40), Math.Min(640, Screen.height - 40));
            rect = GUILayout.Window(WindowId, rect, drawContents, "PERSISTENT CITY · MARKET RESEARCH");
        }

        private void DrawContents(int id)
        {
            var current = view;
            if (current == null) return;
            text ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
            GUILayout.BeginHorizontal();
            GUILayout.Label("Orders and delivered reports");
            if (GUILayout.Button("Close", GUILayout.Width(80))) { Hide(); GUILayout.EndHorizontal(); return; }
            GUILayout.EndHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(Math.Min(180, 28 * Math.Max(1, current.Rows.Count) + 8)));
            for (int i = 0; i < current.Rows.Count; i++)
                if (GUILayout.Button(current.Rows[i], text)) model.Select(current.Ids[i]);
            GUILayout.EndScrollView();
            reportScroll = GUILayout.BeginScrollView(reportScroll);
            GUILayout.Label(current.Detail, text);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }
    }
}
