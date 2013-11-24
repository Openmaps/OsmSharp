﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2013 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using OsmSharp.Collections;
using OsmSharp.Math.Primitives;
using OsmSharp.UI.Renderer.Primitives;
using OsmSharp.UI.Renderer.Scene.Primitives;
using OsmSharp.UI.Renderer.Scene.Styles;
using System.IO;
using OsmSharp.UI.Renderer.Scene.Storage;

namespace OsmSharp.UI.Renderer.Scene
{
    /// <summary>
    /// Contains all objects that need to be rendered.
    /// </summary>
    public class Scene2D : IPrimitives2DSource
    {
        /// <summary>
        /// Holds the string table.
        /// </summary>
        private ObjectTable<string> _stringTable;

        /// <summary>
        /// Holds the zoom ranges.
        /// </summary>
        private ObjectTable<Scene2DZoomRange> _zoomRanges;

        /// <summary>
        /// Holds the index of point.
        /// </summary>
        private ObjectTable<ScenePoint> _pointIndex;

        /// <summary>
        /// Holds the index of points.
        /// </summary>
        private ObjectTable<ScenePoints> _pointsIndex;

        /// <summary>
        /// Holds the point styles.
        /// </summary>
        private ObjectTable<StylePoint> _pointStyles;

        /// <summary>
        /// Holds the text styles.
        /// </summary>
        private ObjectTable<StyleText> _textStyles;

        /// <summary>
        /// Holds the line styles.
        /// </summary>
        private ObjectTable<StyleLine> _lineStyles;

        /// <summary>
        /// Holds the polygon styles.
        /// </summary>
        private ObjectTable<StylePolygon> _polygonStyles;

        /// <summary>
        /// Holds the next id.
        /// </summary>
        private uint _nextId = 0;

        /// <summary>
        /// Holds the scene objects per layer.
        /// </summary>
        private List<Dictionary<uint, SceneObject>> _sceneObjects;

        /// <summary>
        /// Holds the index of images.
        /// </summary>
        private List<byte[]> _imageIndex;

        /// <summary>
        /// Holds the zoom factor cutoffs.
        /// </summary>
        private List<float> _zoomFactors;

        /// <summary>
        /// Creates a new scene that keeps objects per zoom factor (and simplifies them accordingly).
        /// </summary>
        /// <param name="zoomFactors"></param>
        public Scene2D(List<float> zoomFactors)
        {
            _nextId = 0;
            _zoomFactors = zoomFactors;

            // string table.
            _stringTable = new ObjectTable<string>(true);

            // zoom ranges.
            _zoomRanges = new ObjectTable<Scene2DZoomRange>(true);

            // styles.
            _pointStyles = new ObjectTable<StylePoint>(true);
            _textStyles = new ObjectTable<StyleText>(true);
            _lineStyles = new ObjectTable<StyleLine>(true);
            _polygonStyles = new ObjectTable<StylePolygon>(true);

            // geo indexes.
            _pointIndex = new ObjectTable<ScenePoint>(true);
            _pointsIndex = new ObjectTable<ScenePoints>(true);

            // scene objects.
            _sceneObjects = new List<Dictionary<uint, SceneObject>>();
            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                _sceneObjects.Add(new Dictionary<uint, SceneObject>());
            }

            // lines/polygons.
            _imageIndex = new List<byte[]>();
        }

        /// <summary>
        /// Creates a new scene that keeps objects (and simplifies) for one zoom-level.
        /// </summary>
        /// <param name="zoomFactor">The zoomfactor relative to the projection of the objects in the scene.</param>
        public Scene2D(float zoomFactor)
            : this(new List<float>(new float[]{ zoomFactor }))
        {

        }

        /// <summary>
        /// Calculates the simplification epsilon.
        /// </summary>
        /// <returns>The simplification epsilon.</returns>
        /// <param name="zoomFactor">Zoom factor.</param>
        private float CalculateSimplificationEpsilon(float zoomFactor)
        {
            double pixelWidth = (1 / zoomFactor) * 4;
            return (float)pixelWidth;
        }

        /// <summary>
        /// Gets or sets the backcolor.
        /// </summary>
        public int? BackColor { get; set; }

        /// <summary>
        /// Returns the zoom range in an array.
        /// </summary>
        /// <returns></returns>
        internal Scene2DZoomRange[] GetZoomRanges()
        {
            return _zoomRanges.ToArray();
        }

        /// <summary>
        /// Returns the zoom factors in an array.
        /// </summary>
        /// <returns></returns>
        internal float[] GetZoomFactors()
        {
            return _zoomFactors.ToArray();
        }

        /// <summary>
        /// Adds the given point.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public uint AddPoint(double x, double y)
        {
            return _pointIndex.Add(new ScenePoint(x, y));
        }

        /// <summary>
        /// Returns the point with the given id.
        /// </summary>
        /// <param name="pointId"></param>
        /// <returns></returns>
        public Scene2D.ScenePoint GetPoint(uint pointId)
        {
            return _pointIndex.Get(pointId);
        }

        /// <summary>
        /// Adds the given points using the maximum zoom factor as simplification.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>The id of the given points-series or null when the geometry is not visible in this scene.</returns>
        public uint? AddPoints(double[] x, double[] y)
        {
            return this.AddPoints(x, y, _zoomFactors[0]);
        }

        /// <summary>
        /// Returns the points with the given id.
        /// </summary>
        /// <param name="pointId"></param>
        /// <returns></returns>
        public ScenePoints GetPoints(uint pointId)
        {
            return _pointsIndex.Get(pointId);
        }

        /// <summary>
        /// Adds the given points.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="maxZoomFactor">The maximum zoom factor these points are used at. This can maximize potential simplifications.</param>
        /// <returns>The id of the given points-series or null when the geometry is not visible in this scene.</returns>
        public uint? AddPoints(double[] x, double[] y, float maxZoomFactor)
        {
            // get the simplification zoomfactor, always use the max zoom factor as minimum.
            float zoomFactor = _zoomFactors[0];
            if (maxZoomFactor < zoomFactor)
            { // simplify even more! it's allowed!
                zoomFactor = maxZoomFactor;
            }

            // calculate simplification epislon and simplify.
            double epsilon = this.CalculateSimplificationEpsilon(_zoomFactors[0]);
            double[][] simplified = OsmSharp.Math.Algorithms.SimplifyCurve.Simplify(new double[][] { x, y },
                                                            epsilon);
            BoxF2D rectangle = new BoxF2D(x, y);
            if (rectangle.Delta[0] < epsilon && rectangle.Delta[1] < epsilon)
            {
                return null;
            }
            double distance = epsilon * 2;
            if (simplified[0].Length == 2)
            { // check if the simplified version is smaller than epsilon.
                OsmSharp.Math.Primitives.PointF2D point1 = new OsmSharp.Math.Primitives.PointF2D(
                    simplified[0][0], simplified[0][1]);
                OsmSharp.Math.Primitives.PointF2D point2 = new OsmSharp.Math.Primitives.PointF2D(
                    simplified[1][0], simplified[0][1]);
                distance = point1.Distance(point2);
            }
            if (distance >= epsilon)
            {
                return _pointsIndex.Add(new ScenePoints(simplified[0], simplified[1]));
            }
            return null;
        }

        /// <summary>
        /// Adds the given image.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public uint AddImage(byte[] data)
        {
            uint id = (uint)_imageIndex.Count;
            _imageIndex.Add(data);
            return id;
        }

        /// <summary>
        /// Returns an array of images.
        /// </summary>
        /// <returns></returns>
        internal List<byte[]> GetImages()
        {
            return _imageIndex;
        }

        /// <summary>
        /// Returns the image with the given id.
        /// </summary>
        /// <param name="imageId"></param>
        /// <returns></returns>
        public byte[] GetImage(uint imageId)
        {
            return _imageIndex[(int)imageId];
        }

        /// <summary>
        /// Returns the text for the given id.
        /// </summary>
        /// <param name="textId"></param>
        /// <returns></returns>
        public string GetText(uint textId)
        {
            return _stringTable.Get(textId);
        }

        /// <summary>
        /// Clears all data from this scene.
        /// </summary>
        public void Clear()
        {            
            // string table.
            _stringTable.Clear();

            // zoom ranges.
            _zoomRanges.Clear();

            // styles.
            _pointStyles.Clear();
            _textStyles.Clear();
            _lineStyles.Clear();
            _polygonStyles.Clear();

            // geo indexes.
            _pointIndex.Clear();
            _pointsIndex.Clear();

            // scene objects.
            _sceneObjects.Clear();

            // lines/polygons.
            _imageIndex.Clear();
        }

        /// <summary>
        /// Returns the number of objects in this scene.
        /// </summary>
        public int Count
        {
            get
            {
                return (int)_nextId;
            }
        }

        /// <summary>
        /// Returns the objects at a an index corrsponding to a given zoom factor.
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        internal Dictionary<uint, SceneObject> GetObjectsAt(int idx)
        {
            return _sceneObjects[idx];
        }

        /// <summary>
        /// Returns the object with the given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Primitive2D Get(uint id)
        {
            SceneObject sceneObject = null;
            foreach (Dictionary<uint, SceneObject> potentailScene in _sceneObjects)
            {
                if (potentailScene.TryGetValue(id, out sceneObject))
                {
                    break;
                }
            }
            if (sceneObject == null)
            {
                return null;
            }
            return this.ConvertToPrimitive(id, sceneObject);
        }

        /// <summary>
        /// Returns the objects in this scene inside the given view.
        /// </summary>
        /// <param name="view"></param>
        /// <param name="zoom"></param>
        /// <returns></returns>
        public IEnumerable<Primitive2D> Get(View2D view, float zoom)
        {
            SortedSet<Primitive2D> primitives = new SortedSet<Primitive2D>(
                LayerComparer.GetInstance());

            Dictionary<uint, SceneObject> sceneAtZoom = _sceneObjects[0];
            // find the part of this scene containing the objects for the requested zoom.
            for (int idx = 1; idx < _zoomFactors.Count; idx++)
            {
                if (zoom <= _zoomFactors[idx])
                {
                    sceneAtZoom = _sceneObjects[idx];
                }
                else
                {
                    break;
                }
            }

            if (sceneAtZoom != null)
            {
                foreach (KeyValuePair<uint, SceneObject> sceneObjectPair in sceneAtZoom)
                { // loop over all primitives in order.
                    SceneObject sceneObject = sceneObjectPair.Value;
                    uint id = sceneObjectPair.Key;

                    ScenePoint point;
                    ScenePoints points;
                    switch (sceneObject.Enum)
                    {
                        case SceneObjectType.IconObject:
                            SceneIconObject icon = sceneObject as SceneIconObject;
                            if (_zoomRanges.Get(icon.ZoomRangeId).Contains(zoom))
                            {
                                point = _pointIndex.Get(icon.GeoId);
                                if (view.Contains(point.X, point.Y))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, icon));
                                }
                            }
                            break;
                        case SceneObjectType.LineObject:
                            SceneLineObject line = sceneObject as SceneLineObject;
                            points = _pointsIndex.Get(line.GeoId);
                            if (_zoomRanges.Get(line.ZoomRangeId).Contains(zoom))
                            {
                                if (view.IsVisible(points.X, points.Y, false))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, line));
                                }
                            }
                            break;
                        case SceneObjectType.LineTextObject:
                            SceneLineTextObject lineText = sceneObject as SceneLineTextObject;
                            points = _pointsIndex.Get(lineText.GeoId);
                            if (_zoomRanges.Get(lineText.ZoomRangeId).Contains(zoom))
                            {
                                if (view.IsVisible(points.X, points.Y, false))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, lineText));
                                }
                            }
                            break;
                        case SceneObjectType.PointObject:
                            ScenePointObject pointObject = sceneObject as ScenePointObject;
                            point = _pointIndex.Get(pointObject.GeoId);
                            if (_zoomRanges.Get(pointObject.ZoomRangeId).Contains(zoom))
                            {
                                if (view.Contains(point.X, point.Y))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, pointObject));
                                }
                            }
                            break;
                        case SceneObjectType.PolygonObject:
                            ScenePolygonObject polygonObject = sceneObject as ScenePolygonObject;
                            points = _pointsIndex.Get(polygonObject.GeoId);
                            if (_zoomRanges.Get(polygonObject.ZoomRangeId).Contains(zoom))
                            {
                                if (view.IsVisible(points.X, points.Y, false))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, polygonObject));
                                }
                            }
                            break;
                        case SceneObjectType.TextObject:
                            SceneTextObject textObject = sceneObject as SceneTextObject;
                            point = _pointIndex.Get(textObject.GeoId);
                            if (_zoomRanges.Get(textObject.ZoomRangeId).Contains(zoom))
                            {
                                if (view.Contains(point.X, point.Y))
                                {
                                    primitives.Add(
                                        this.ConvertToPrimitive(id, textObject));
                                }
                            }
                            break;
                    }
                }
            }

            return primitives;
        }

        /// <summary>
        /// Converts the given scene object to a primitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, SceneObject sceneObject)
        {
            switch (sceneObject.Enum)
            {
                case SceneObjectType.IconObject:
                    return this.ConvertToPrimitive(id, sceneObject as SceneIconObject);
                case SceneObjectType.LineObject:
                    return this.ConvertToPrimitive(id, sceneObject as SceneLineObject);
                case SceneObjectType.LineTextObject:
                    return this.ConvertToPrimitive(id, sceneObject as SceneLineTextObject);
                case SceneObjectType.PointObject:
                    return this.ConvertToPrimitive(id, sceneObject as ScenePointObject);
                case SceneObjectType.PolygonObject:
                    return this.ConvertToPrimitive(id, sceneObject as ScenePolygonObject);
                case SceneObjectType.TextObject:
                    return this.ConvertToPrimitive(id, sceneObject as SceneTextObject);
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Serializes this scene into the given stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compress"></param>
        public void Serialize(Stream stream, bool compress)
        {
            SceneSerializer.Serialize(stream, this, compress);
        }

        /// <summary>
        /// Deserializes a primitive source from a stream of a previously serialized scene.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compress"></param>
        /// <returns></returns>
        public static IPrimitives2DSource Deserialize(Stream stream, bool compress)
        {
            return SceneSerializer.Deserialize(stream, compress);
        }

        /// <summary>
        /// A scene point class.
        /// </summary>
        public class ScenePoint
        {
            /// <summary>
            /// Creates a new scene point.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            public ScenePoint(double x, double y)
            {
                this.X = x;
                this.Y = y;
            }

            public double X { get; set; }

            public double Y { get; set; }

            public override bool Equals(object obj)
            {
                ScenePoint other = obj as ScenePoint;
                if (obj != null)
                {
                    return other.X == this.X &&
                        other.Y == this.Y;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return this.X.GetHashCode() ^
                    this.Y.GetHashCode();
            }
        }

        /// <summary>
        /// A scene point class.
        /// </summary>
        public class ScenePoints
        {
            public ScenePoints(double[] x, double[] y)
            {
                this.X = x;
                this.Y = y;
            }

            public double[] X { get; set; }

            public double[] Y { get; set; }

            public override bool Equals(object obj)
            {
                ScenePoints other = obj as ScenePoints;
                if (obj != null)
                {
                    for (int idx = 0; idx < this.X.Length; idx++)
                    {
                        if (other.X[idx] != this.X[idx] ||
                            other.Y[idx] != this.Y[idx])
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            public override int GetHashCode()
            {
                int hash = 0;
                for (int idx = 0; idx < this.X.Length; idx++)
                {
                    hash = hash ^
                        this.X[idx].GetHashCode() ^
                        this.Y[idx].GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// Layer comparer to sort objects by layer.
        /// </summary>
        private class LayerComparer : IComparer<Primitive2D>
        {
            private static LayerComparer _instance = null;

            public static LayerComparer GetInstance()
            {
                if (_instance == null)
                {
                    _instance = new LayerComparer();
                }
                return _instance;
            }

            public int Compare(Primitive2D x, Primitive2D y)
            {
                if (x.Layer == y.Layer)
                { // objects with same layer, assume different.
                    return -1;
                }
                return x.Layer.CompareTo(y.Layer);
            }
        }

        #region Styles

        /// <summary>
        /// Adds the given style to the given point.
        /// </summary>
        /// <param name="pointId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="color"></param>
        /// <param name="size"></param>
        public List<uint> AddStylePoint(uint pointId, uint layer, float minZoom, float maxZoom, int color, float size)
        { // add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoint pointPair = _pointIndex.Get(pointId);

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);
                    // add to the scene.
                    // build the zoom range.
                    Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                    {
                        MinZoom = minimumZoomFactor,
                        MaxZoom = maximumZoomFactor
                    };
                    uint zoomRangeId = _zoomRanges.Add(zoomRange);

                    // build the style.
                    StylePoint style = new StylePoint()
                    {
                        Color = color,
                        Size = size
                    };
                    uint styleId = _pointStyles.Add(style);

                    // add the scene object.
                    uint id = _nextId;
                    _sceneObjects[idx].Add(id, 
                        new ScenePointObject() { StyleId = styleId, Layer = layer, GeoId = pointId, ZoomRangeId = zoomRangeId });
                    _nextId++;
                    newIds.Add(id);
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns an array of all style points.
        /// </summary>
        /// <returns></returns>
        internal StylePoint[] GetStylePoints()
        {
            return _pointStyles.ToArray();
        }

        /// <summary>
        /// Returns the point style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StylePoint GetStylePoint(uint styleId)
        {
            return _pointStyles.Get(styleId);
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, ScenePointObject sceneObject)
        {
            Point2D point = new Point2D();
            point.Id = id;
            point.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            point.MaxZoom = zoomRange.MaxZoom;
            point.MinZoom = zoomRange.MinZoom;

            // convert style.
            StylePoint style = _pointStyles.Get(sceneObject.StyleId);
            point.Color = style.Color;
            point.Size = style.Size;

            // get the geo.
            ScenePoint geo = _pointIndex.Get(sceneObject.GeoId);
            point.X = geo.X;
            point.Y = geo.Y;

            return point;
        }

        /// <summary>
        /// Adds a new icon at the location of the given point.
        /// </summary>
        /// <param name="pointId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="imageId"></param>
        /// <returns></returns>
        public List<uint> AddIcon(uint pointId, uint layer, float minZoom, float maxZoom, uint imageId)
        { // add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoint pointPair = _pointIndex.Get(pointId);

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);
                    // add to the scene.
                    // build the zoom range.
                    Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                    {
                        MinZoom = minimumZoomFactor,
                        MaxZoom = maximumZoomFactor
                    };
                    uint zoomRangeId = _zoomRanges.Add(zoomRange);

                    // add the scene object.
                    uint id = _nextId;
                    _sceneObjects[idx].Add(id, 
                        new SceneIconObject() { StyleId = imageId, Layer = layer, GeoId = pointId, ZoomRangeId = zoomRangeId });
                    _nextId++;
                    newIds.Add(id);
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns an array of all style points.
        /// </summary>
        /// <returns></returns>
        internal byte[][] GetStyleIcons()
        {
            return _imageIndex.ToArray();
        }

        /// <summary>
        /// Returns the icon style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public uint GetStyleIcon(uint styleId)
        {
            return styleId;
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, SceneIconObject sceneObject)
        {
            Icon2D primitive = new Icon2D();
            primitive.Id = id;
            primitive.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            primitive.MaxZoom = zoomRange.MaxZoom;
            primitive.MinZoom = zoomRange.MinZoom;

            // convert image.
            byte[] style = _imageIndex[(int)sceneObject.StyleId];
            primitive.Image = style;

            // get the geo.
            ScenePoint geo = _pointIndex.Get(sceneObject.GeoId);
            primitive.X = geo.X;
            primitive.Y = geo.Y;

            return primitive;
        }

        /// <summary>
        /// Adds text at the position of the given point.
        /// </summary>
        /// <param name="pointId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="size"></param>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="haloColor"></param>
        /// <param name="haloRadius"></param>
        /// <param name="font"></param>
        /// <returns></returns>
        public List<uint> AddText(uint pointId, uint layer, float minZoom, float maxZoom, float size, string text, int color, 
            int? haloColor, int? haloRadius, string font)
        { // add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoint pointPair = _pointIndex.Get(pointId);

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);
                    // add to the scene.
                    // build the zoom range.
                    Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                    {
                        MinZoom = minimumZoomFactor,
                        MaxZoom = maximumZoomFactor
                    };
                    uint zoomRangeId = _zoomRanges.Add(zoomRange);

                    // add to stringtable.
                    uint textId = _stringTable.Add(text);

                    // build the style.
                    StyleText style = new StyleText()
                    {
                        Color = color,
                        Size = size,
                        Font = font,
                        HaloColor = haloColor,
                        HaloRadius = haloRadius
                    };
                    uint styleId = _textStyles.Add(style);

                    // add the scene object.
                    uint id = _nextId;
                    _sceneObjects[idx].Add(id, 
                        new SceneTextObject() { StyleId = styleId, Layer = layer, GeoId = pointId, ZoomRangeId = zoomRangeId, TextId = textId });
                    _nextId++;
                    newIds.Add(id);
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns an array of all style texts.
        /// </summary>
        /// <returns></returns>
        internal StyleText[] GetStyleTexts()
        {
            return _textStyles.ToArray();
        }

        /// <summary>
        /// Returns the text style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StyleText GetStyleText(uint styleId)
        {
            return _textStyles.Get(styleId);
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, SceneTextObject sceneObject)
        {
            Text2D primitive = new Text2D();
            primitive.Id = id;
            primitive.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            primitive.MaxZoom = zoomRange.MaxZoom;
            primitive.MinZoom = zoomRange.MinZoom;

            // convert image.
            StyleText style = _textStyles.Get(sceneObject.StyleId);
            primitive.Color = style.Color;
            primitive.Font = style.Font;
            primitive.HaloColor = style.HaloColor;
            primitive.HaloRadius = style.HaloRadius;
            primitive.Size = style.Size;

            // get the text.
            primitive.Text = _stringTable.Get(sceneObject.TextId);

            // get the geo.
            ScenePoint geo = _pointIndex.Get(sceneObject.GeoId);
            primitive.X = geo.X;
            primitive.Y = geo.Y;

            return primitive;
        }

        /// <summary>
        /// Adds a line with the given points and style.
        /// </summary>
        /// <param name="pointsId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="color"></param>
        /// <param name="width"></param>
        /// <param name="lineJoin"></param>
        /// <param name="dashes"></param>
        public List<uint> AddStyleLine(uint pointsId, uint layer, float minZoom, float maxZoom, int color, float width, LineJoin lineJoin, int[] dashes)
        {// add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoints pointsPair = _pointsIndex.Get(pointsId);
            double[][] points = new double[][] { pointsPair.X, pointsPair.Y };

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);

                    // simplify the algorithm.
                    double epsilon = this.CalculateSimplificationEpsilon(
                        System.Math.Min(simplificationFactor, maximumZoomFactor));
                    double[][] simplified = OsmSharp.Math.Algorithms.SimplifyCurve.Simplify(points,
                                                                    epsilon);
                    double distance = epsilon * 2;
                    if (simplified[0].Length == 2)
                    { // check if the simplified version is smaller than epsilon.
                        OsmSharp.Math.Primitives.PointF2D point1 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[0][0], simplified[0][1]);
                        OsmSharp.Math.Primitives.PointF2D point2 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[1][0], simplified[0][1]);
                        distance = point1.Distance(point2);
                    }
                    if (distance >= epsilon)
                    { // the object needs to be added for the current zoom range.
                        uint geometryId = pointsId;
                        // check if there is a need to add a simplified geometry.
                        if (simplified[0].Length < points[0].Length)
                        { // add a new simplified geometry.
                            geometryId = _pointsIndex.Add(new ScenePoints(simplified[0], simplified[1]));
                        }

                        // add to the scene.
                        // build the zoom range.
                        Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                        {
                            MinZoom = minimumZoomFactor,
                            MaxZoom = maximumZoomFactor
                        };
                        uint zoomRangeId = _zoomRanges.Add(zoomRange);

                        // build the style.
                        StyleLine style = new StyleLine()
                        {
                            Color = color,
                            Dashes = dashes,
                            LineJoin = lineJoin,
                            Width = width
                        };
                        uint styleId = _lineStyles.Add(style);

                        // add the scene object.
                        uint id = _nextId;
                        _sceneObjects[idx].Add(id, 
                            new SceneLineObject() { StyleId = styleId, Layer = layer, GeoId = pointsId, ZoomRangeId = zoomRangeId });
                        _nextId++;
                        newIds.Add(id);
                    }
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns the line style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StyleLine[] GetStyleLines()
        {
            return _lineStyles.ToArray();
        }

        /// <summary>
        /// Returns the line style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StyleLine GetStyleLine(uint styleId)
        {
            return _lineStyles.Get(styleId);
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, SceneLineObject sceneObject)
        {
            Line2D primitive = new Line2D();
            primitive.Id = id;
            primitive.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            primitive.MaxZoom = zoomRange.MaxZoom;
            primitive.MinZoom = zoomRange.MinZoom;

            // convert image.
            StyleLine style = _lineStyles.Get(sceneObject.StyleId);
            primitive.Color = style.Color;
            primitive.LineJoin = style.LineJoin;
            primitive.Width = style.Width;
            primitive.Dashes = style.Dashes;

            // get the geo.
            ScenePoints geo = _pointsIndex.Get(sceneObject.GeoId);
            primitive.X = geo.X;
            primitive.Y = geo.Y;

            return primitive;
        }

        /// <summary>
        /// Adds a line text with the given points and style.
        /// </summary>
        /// <param name="pointsId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="color"></param>
        /// <param name="size"></param>
        /// <param name="text"></param>
        /// <param name="font"></param>
        /// <param name="haloColor"></param>
        /// <param name="haloRadius"></param>
        public List<uint> AddStyleLineText(uint pointsId, uint layer, float minZoom, float maxZoom, int color, float size, string text, string font,
            int? haloColor, int? haloRadius)
        { // add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoints pointsPair = _pointsIndex.Get(pointsId);
            double[][] points = new double[][] { pointsPair.X, pointsPair.Y };

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);

                    // simplify the algorithm.
                    double epsilon = this.CalculateSimplificationEpsilon(
                        System.Math.Min(simplificationFactor, maximumZoomFactor));
                    double[][] simplified = OsmSharp.Math.Algorithms.SimplifyCurve.Simplify(points,
                                                                    epsilon);
                    double distance = epsilon * 2;
                    if (simplified[0].Length == 2)
                    { // check if the simplified version is smaller than epsilon.
                        OsmSharp.Math.Primitives.PointF2D point1 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[0][0], simplified[0][1]);
                        OsmSharp.Math.Primitives.PointF2D point2 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[1][0], simplified[0][1]);
                        distance = point1.Distance(point2);
                    }
                    if (distance >= epsilon)
                    { // the object needs to be added for the current zoom range.
                        uint geometryId = pointsId;
                        // check if there is a need to add a simplified geometry.
                        if (simplified[0].Length < points[0].Length)
                        { // add a new simplified geometry.
                            geometryId = _pointsIndex.Add(new ScenePoints(simplified[0], simplified[1]));
                        }

                        // add to the scene.
                        // build the zoom range.
                        Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                        {
                            MinZoom = minimumZoomFactor,
                            MaxZoom = maximumZoomFactor
                        };
                        uint zoomRangeId = _zoomRanges.Add(zoomRange);

                        // add to stringtable.
                        uint textId = _stringTable.Add(text);

                        // build the style.
                        StyleText style = new StyleText()
                        {
                            Color = color,
                            Size = size,
                            Font = font,
                            HaloColor = haloColor,
                            HaloRadius = haloRadius
                        };
                        uint styleId = _textStyles.Add(style);

                        // add the scene object.
                        uint id = _nextId;
                        _sceneObjects[idx].Add(id, 
                            new SceneLineTextObject() { StyleId = styleId, Layer = layer, GeoId = pointsId, ZoomRangeId = zoomRangeId, TextId = textId });
                        _nextId++;
                        newIds.Add(id);
                    }
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns the line text style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StyleText[] GetStyleLineTexts()
        {
            return _textStyles.ToArray();
        }

        /// <summary>
        /// Returns the line text style for the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StyleText GetStyleLineText(uint styleId)
        {
            return _textStyles.Get(styleId);
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, SceneLineTextObject sceneObject)
        {
            LineText2D primitive = new LineText2D();
            primitive.Id = id;
            primitive.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            primitive.MaxZoom = zoomRange.MaxZoom;
            primitive.MinZoom = zoomRange.MinZoom;

            // convert image.
            StyleText style = _textStyles.Get(sceneObject.StyleId);
            primitive.Color = style.Color;
            primitive.Font = style.Font;
            primitive.HaloColor = style.HaloColor;
            primitive.HaloRadius = style.HaloRadius;
            primitive.Size = style.Size;

            // get the text.
            primitive.Text = _stringTable.Get(sceneObject.TextId);

            // get the geo.
            ScenePoints geo = _pointsIndex.Get(sceneObject.GeoId);
            primitive.X = geo.X;
            primitive.Y = geo.Y;

            return primitive;
        }

        /// <summary>
        /// Adds a polygon with the given points and style.
        /// </summary>
        /// <param name="pointsId"></param>
        /// <param name="layer"></param>
        /// <param name="minZoom"></param>
        /// <param name="maxZoom"></param>
        /// <param name="color"></param>
        /// <param name="width"></param>
        /// <param name="fill"></param>
        public List<uint> AddStylePolygon(uint pointsId, uint layer, float minZoom, float maxZoom, int color, float width, bool fill)
        { // add the line but simplify it for higher zoom levels.
            List<uint> newIds = new List<uint>();
            // get the geometry.
            ScenePoints pointsPair = _pointsIndex.Get(pointsId);
            double[][] points = new double[][] { pointsPair.X, pointsPair.Y };

            for (int idx = 0; idx < _zoomFactors.Count; idx++)
            {
                // get the simplification factor.
                float simplificationFactor = _zoomFactors[idx];

                // get the minimum zoom factor.
                float minimumZoomFactor = float.MinValue;
                if (idx + 1 < _zoomFactors.Count)
                { // the next minification is the minimum zoom factor.
                    minimumZoomFactor = _zoomFactors[idx + 1];
                }

                // get the maximum zoom factor.
                float maximumZoomFactor = float.MaxValue;
                if (idx - 1 > 0)
                { // the previous minification is the maximum zoom factor.
                    maximumZoomFactor = _zoomFactors[idx - 1];
                }

                // check the current object's zoom range against the current min/max zoom factor.
                if (!(minimumZoomFactor >= maxZoom) && !(maximumZoomFactor < minZoom))
                { // ok this object does existing inside the current range.
                    // limit the object's zoom.
                    minimumZoomFactor = System.Math.Max(minimumZoomFactor, minZoom);
                    maximumZoomFactor = System.Math.Min(maximumZoomFactor, maxZoom);

                    // simplify the algorithm.
                    double epsilon = this.CalculateSimplificationEpsilon(
                        System.Math.Min(simplificationFactor, maximumZoomFactor));
                    double[][] simplified = OsmSharp.Math.Algorithms.SimplifyCurve.Simplify(points,
                                                                    epsilon);
                    double distance = epsilon * 2;
                    if (simplified[0].Length == 2)
                    { // check if the simplified version is smaller than epsilon.
                        OsmSharp.Math.Primitives.PointF2D point1 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[0][0], simplified[0][1]);
                        OsmSharp.Math.Primitives.PointF2D point2 = new OsmSharp.Math.Primitives.PointF2D(
                            simplified[1][0], simplified[0][1]);
                        distance = point1.Distance(point2);
                    }
                    if (distance >= epsilon)
                    { // the object needs to be added for the current zoom range.
                        uint geometryId = pointsId;
                        // check if there is a need to add a simplified geometry.
                        if (simplified[0].Length < points[0].Length)
                        { // add a new simplified geometry.
                            geometryId = _pointsIndex.Add(new ScenePoints(simplified[0], simplified[1]));
                        }

                        // add to the scene.
                        // build the zoom range.
                        Scene2DZoomRange zoomRange = new Scene2DZoomRange()
                        {
                            MinZoom = minimumZoomFactor,
                            MaxZoom = maximumZoomFactor
                        };
                        uint zoomRangeId = _zoomRanges.Add(zoomRange);

                        // build the style.
                        StylePolygon style = new StylePolygon()
                        {
                            Color = color,
                            Fill = fill,
                            Width = width
                        };
                        uint styleId = _polygonStyles.Add(style);

                        // add the scene object.
                        uint id = _nextId;
                        _sceneObjects[idx].Add(id, 
                            new ScenePolygonObject() { StyleId = styleId, Layer = layer, GeoId = pointsId, ZoomRangeId = zoomRangeId });
                        _nextId++;
                        newIds.Add(id);
                    }
                }
            }
            return newIds;
        }

        /// <summary>
        /// Returns the polygon style of the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StylePolygon[] GetStylePolygons()
        {
            return _polygonStyles.ToArray();
        }

        /// <summary>
        /// Returns the polygon style of the given id.
        /// </summary>
        /// <param name="styleId"></param>
        /// <returns></returns>
        public StylePolygon GetStylePolygon(uint styleId)
        {
            return _polygonStyles.Get(styleId);
        }

        /// <summary>
        /// Converts the given object to a Scene2DPrimitive.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneObject"></param>
        /// <returns></returns>
        private Primitive2D ConvertToPrimitive(uint id, ScenePolygonObject sceneObject)
        {
            Polygon2D primitive = new Polygon2D();
            primitive.Id = id;
            primitive.Layer = sceneObject.Layer;

            // convert zoom range.
            Scene2DZoomRange zoomRange = _zoomRanges.Get(sceneObject.ZoomRangeId);
            primitive.MaxZoom = zoomRange.MaxZoom;
            primitive.MinZoom = zoomRange.MinZoom;

            // convert image.
            StylePolygon style = _polygonStyles.Get(sceneObject.StyleId);
            primitive.Color = style.Color;
            primitive.Fill = style.Fill;
            primitive.Width = style.Width;

            // get the geo.
            ScenePoints geo = _pointsIndex.Get(sceneObject.GeoId);
            primitive.X = geo.X;
            primitive.Y = geo.Y;

            return primitive;
        }

        #endregion
    }
}