using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.UI;
using Eto.Forms;
using Eto.Drawing;

namespace EnhancedLayerStates
{
    // The Unique ID that Rhino uses to remember where you docked this panel
    [System.Runtime.InteropServices.Guid("59E098F3-431B-4A73-BC30-A1D6E266041D")]
    public class EnhancedLayerStatePanel : Panel
    {
        private ListBox _listBox;
        private SearchBox _searchBox;
        private TextBox _nameInput;
        private List<string> _allStates = new List<string>();

        public EnhancedLayerStatePanel(uint documentSerialNumber)
        {
            var layout = new DynamicLayout { Padding = 10, Spacing = new Size(5, 5) };

            _searchBox = new SearchBox { PlaceholderText = "Search states..." };
            _searchBox.TextChanged += (s, e) => FilterList();

            _listBox = new ListBox { Size = new Size(-1, 200) };
            _nameInput = new TextBox { PlaceholderText = "New State Name..." };

            var btnSave = new Button { Text = "SAVE/UPDATE" };
            btnSave.Click += OnSave;

            var btnRestore = new Button { Text = "RESTORE TO DETAIL" };
            btnRestore.Click += OnRestore;

            layout.AddSeparateRow(_searchBox);
            layout.AddSeparateRow(_listBox);
            layout.AddSeparateRow(_nameInput);
            layout.AddRow(btnSave, btnRestore);
            
            Content = layout;
            RefreshList();
        }

        private void FilterList()
        {
            var filter = _searchBox.Text?.ToLower() ?? "";
            _listBox.DataStore = string.IsNullOrEmpty(filter) 
                ? _allStates 
                : _allStates.Where(x => x.ToLower().Contains(filter)).ToList();
        }

        private void RefreshList()
        {
            var doc = RhinoDoc.ActiveDoc;
            string raw = doc?.Strings.GetValue("ENHANCED_LS_INDEX") ?? "";
            _allStates = !string.IsNullOrEmpty(raw) ? raw.Split('|').OrderBy(x => x).ToList() : new List<string>();
            FilterList();
        }

        private void OnSave(object sender, EventArgs e)
        {
            string name = _nameInput.Text;
            if (string.IsNullOrEmpty(name)) return;
            RhinoApp.RunScript($@"-_LayerState _Save ""{name}"" _Enter _Enter", false);
            
            var doc = RhinoDoc.ActiveDoc;
            if (!_allStates.Contains(name)) _allStates.Add(name);
            doc.Strings.SetString("ENHANCED_LS_INDEX", string.Join("|", _allStates));
            _nameInput.Text = "";
            RefreshList();
        }

        private void OnRestore(object sender, EventArgs e)
        {
            if (_listBox.SelectedValue == null) return;
            string state = _listBox.SelectedValue.ToString();
            if (Rhino.Input.RhinoGet.GetOneObject("Select Detail", false, Rhino.DocObjects.ObjectType.Detail, out var objRef) == Rhino.Commands.Result.Success)
            {
                RhinoApp.RunScript($@"-_LayerState _Restore ""{state}"" _RestoreMode=_Detail _SelectionOnly=_Yes _Enter _Enter", false);
                UpdateTrackerDot(objRef.ObjectId, state);
            }
        }

        private void UpdateTrackerDot(Guid detailId, string stateName)
        {
            var doc = RhinoDoc.ActiveDoc;
            var oldDots = doc.Objects.FindByUserString("SourceDetailID", detailId.ToString(), false);
            foreach (var d in oldDots) doc.Objects.Delete(d, true);

            var detailObj = doc.Objects.FindId(detailId);
            var bbox = detailObj.Geometry.GetBoundingBox(true);
            
            var dot = new Rhino.Geometry.TextDot($"LS: {stateName}", bbox.Min);
            var attr = new Rhino.DocObjects.ObjectAttributes();
            attr.SetUserString("SourceDetailID", detailId.ToString());
            
            doc.Objects.AddTextDot(dot, attr);
            detailObj.Attributes.SetUserString("LastAppliedState", stateName);
            detailObj.CommitChanges();
            doc.Views.Redraw();
        }
    }
}
