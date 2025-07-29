using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Schema.Core.Data;

namespace Schema.Unity.Editor
{
    /// <summary>
    /// Handles virtual scrolling for large table views to improve performance
    /// </summary>
    public class VirtualTableView
    {
        #region Constants
        
        private const float DEFAULT_ROW_HEIGHT = 20f;
        private const float BUFFER_ROWS = 0f; // Number of extra rows to render above/below visible area
        
        #endregion
        
        #region Fields
        
        private readonly float _rowHeight;
        private readonly float _bufferHeight;
        private Vector2 _lastScrollPosition;
        private Rect _lastViewportRect;
        private int _lastTotalRows;
        private string _lastSchemeName;
        private AttributeSortOrder _lastSortOrder;
        private Dictionary<string, string> _lastFilters;
        
        // Cached visible range
        private int _visibleStartIndex;
        private int _visibleEndIndex;
        private float _totalContentHeight;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets the current visible row range
        /// </summary>
        public (int start, int end) VisibleRange => (_visibleStartIndex, _visibleEndIndex);
        
        /// <summary>
        /// Gets the total height of all content
        /// </summary>
        public float TotalContentHeight => _totalContentHeight;
        
        /// <summary>
        /// Gets whether the virtual scrolling is active (when there are many rows)
        /// </summary>
        public bool IsVirtualScrollingActive { get; private set; }
        
        #endregion
        
        #region Constructor
        
        public VirtualTableView(float rowHeight = DEFAULT_ROW_HEIGHT)
        {
            _rowHeight = rowHeight;
            _bufferHeight = _rowHeight * BUFFER_ROWS;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Calculates the visible row range based on scroll position and viewport
        /// </summary>
        /// <param name="scrollPosition">Current scroll position</param>
        /// <param name="viewportRect">Viewport rectangle</param>
        /// <param name="totalRows">Total number of rows</param>
        /// <param name="schemeName">Current scheme name for cache invalidation</param>
        /// <param name="sortOrder">Current sort order for cache invalidation</param>
        /// <param name="filters">Current filters for cache invalidation</param>
        /// <returns>Tuple of (startIndex, endIndex) for visible rows</returns>
        public (int start, int end) CalculateVisibleRange(
            Vector2 scrollPosition, 
            Rect viewportRect, 
            int totalRows,
            string schemeName,
            AttributeSortOrder sortOrder,
            Dictionary<string, string> filters)
        {
            // Check if we need to recalculate (cache invalidation)
            bool needsRecalculation = 
                _lastTotalRows != totalRows ||
                _lastSchemeName != schemeName ||
                !_lastSortOrder.Equals(sortOrder) ||
                !AreFiltersEqual(_lastFilters, filters) ||
                Math.Abs(_lastScrollPosition.y - scrollPosition.y) > _rowHeight ||
                _lastViewportRect.height != viewportRect.height;
            
            if (!needsRecalculation)
            {
                return (_visibleStartIndex, _visibleEndIndex);
            }
            
            // Update cached values
            _lastScrollPosition = scrollPosition;
            _lastViewportRect = viewportRect;
            _lastTotalRows = totalRows;
            _lastSchemeName = schemeName;
            _lastSortOrder = sortOrder;
            _lastFilters = filters != null ? new Dictionary<string, string>(filters) : null;
            
            // Calculate total content height
            _totalContentHeight = totalRows * _rowHeight;
            
            // Determine if virtual scrolling should be active
            IsVirtualScrollingActive = totalRows > 20; // Threshold for virtual scrolling
            
            if (!IsVirtualScrollingActive)
            {
                // Return full range when virtual scrolling is not needed
                _visibleStartIndex = 0;
                _visibleEndIndex = totalRows;
                return (_visibleStartIndex, _visibleEndIndex);
            }
            
            // Calculate visible range with buffer
            float scrollY = scrollPosition.y;
            float viewportHeight = viewportRect.height;
            
            // Calculate start index with buffer
            int startIndex = Math.Max(0, Mathf.FloorToInt((scrollY - _bufferHeight - 60) / _rowHeight));
            
            // Calculate end index with buffer
            // int endIndex = Math.Min(totalRows, Mathf.CeilToInt((scrollY + viewportHeight - _bufferHeight - 60) / _rowHeight));
            int endIndex = Math.Min(totalRows, Mathf.CeilToInt((scrollY + _bufferHeight + 180) / _rowHeight) + 20);
            // int endIndex = Math.Min(totalRows, startIndex + 20);
            
            // Ensure we always render at least a few rows
            // if (endIndex - startIndex < 10)
            // {
            //     endIndex = Math.Min(totalRows, startIndex + 10);
            // }
            
            _visibleStartIndex = startIndex;
            _visibleEndIndex = endIndex;
            
            return (_visibleStartIndex, _visibleEndIndex);
        }
        
        /// <summary>
        /// Renders spacer elements to maintain proper scroll height
        /// </summary>
        /// <param name="startIndex">Start index of visible range</param>
        /// <param name="totalRows">Total number of rows</param>
        public void RenderSpacers(int startIndex, int totalRows)
        {
            if (!IsVirtualScrollingActive) return;
            
            // Render top spacer
            if (startIndex > 0)
            {
                GUILayout.Space(startIndex * _rowHeight);
            }
        }
        
        /// <summary>
        /// Renders bottom spacer to maintain proper scroll height
        /// </summary>
        /// <param name="endIndex">End index of visible range</param>
        /// <param name="totalRows">Total number of rows</param>
        public void RenderBottomSpacer(int endIndex, int totalRows)
        {
            if (!IsVirtualScrollingActive) return;
            
            // Render bottom spacer
            int remainingRows = totalRows - endIndex;
            if (remainingRows > 0)
            {
                GUILayout.Space(remainingRows * _rowHeight);
            }
        }
        
        /// <summary>
        /// Gets the filtered and sorted entries for the visible range
        /// </summary>
        /// <param name="allEntries">All entries from the scheme</param>
        /// <param name="visibleRange">Visible range tuple</param>
        /// <returns>Entries to render</returns>
        public IEnumerable<DataEntry> GetVisibleEntries(IEnumerable<DataEntry> allEntries, (int start, int end) visibleRange)
        {
            if (!IsVirtualScrollingActive)
            {
                return allEntries;
            }
            
            return allEntries.Skip(visibleRange.start).Take(visibleRange.end - visibleRange.start);
        }
        
        /// <summary>
        /// Clears the cache to force recalculation
        /// </summary>
        public void ClearCache()
        {
            _lastScrollPosition = Vector2.zero;
            _lastViewportRect = Rect.zero;
            _lastTotalRows = 0;
            _lastSchemeName = null;
            _lastSortOrder = AttributeSortOrder.None;
            _lastFilters = null;
            _visibleStartIndex = 0;
            _visibleEndIndex = 0;
            _totalContentHeight = 0f;
        }
        
        #endregion
        
        #region Private Methods
        
        private bool AreFiltersEqual(Dictionary<string, string> filters1, Dictionary<string, string> filters2)
        {
            if (filters1 == null && filters2 == null) return true;
            if (filters1 == null || filters2 == null) return false;
            if (filters1.Count != filters2.Count) return false;
            
            foreach (var kvp in filters1)
            {
                if (!filters2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        #endregion
    }
} 