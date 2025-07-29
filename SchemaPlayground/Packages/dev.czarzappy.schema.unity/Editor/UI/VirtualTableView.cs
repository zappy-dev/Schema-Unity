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
        
        /// <summary>
        /// Gets the number of cells that were drawn in the last render
        /// </summary>
        public int CellsDrawn { get; private set; }
        
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
            
            // Debug logging for scroll calculation
            Debug.Log($"VirtualTableView: Received - scrollY={scrollY:F1}, viewportHeight={viewportHeight:F1}, totalRows={totalRows}, rowHeight={_rowHeight:F1}");
            Debug.Log($"VirtualTableView: Viewport rect received - {viewportRect}");
            
            // Calculate start index with buffer
            // Remove the hardcoded 60-pixel offset that was causing scroll position mismatch
            int startIndex = Math.Max(0, Mathf.FloorToInt((scrollY - _bufferHeight) / _rowHeight));
            
            // Calculate how many rows can actually fit in the viewport
            int rowsThatCanFit = Mathf.CeilToInt(viewportHeight / _rowHeight);
            Debug.Log($"VirtualTableView: viewportHeight={viewportHeight:F1}, rowHeight={_rowHeight:F1}, rowsThatCanFit={rowsThatCanFit}");
            
            // Calculate end index with buffer
            int endIndex = Math.Min(totalRows, Mathf.CeilToInt((scrollY + viewportHeight + _bufferHeight) / _rowHeight));
            
            Debug.Log($"VirtualTableView: startIndex={startIndex}, endIndex={endIndex}, calculated range={startIndex}-{endIndex}");
            
            // Ensure we render enough rows to fill the viewport, but never more than what fits
            int minRowsToRender = Math.Min(rowsThatCanFit, totalRows - startIndex);
            int calculatedRange = endIndex - startIndex;
            
            Debug.Log($"VirtualTableView: minRowsToRender={minRowsToRender}, calculatedRange={calculatedRange}");
            
            // If we're not rendering enough rows to fill the viewport, expand the range
            if (calculatedRange < minRowsToRender)
            {
                endIndex = Math.Min(totalRows, startIndex + minRowsToRender);
                Debug.Log($"VirtualTableView: Expanded range to fill viewport: {startIndex}-{endIndex}");
            }
            // If we're rendering more rows than can fit, limit the range
            else if (calculatedRange > rowsThatCanFit)
            {
                endIndex = Math.Min(totalRows, startIndex + rowsThatCanFit);
                Debug.Log($"VirtualTableView: Limited range to fit viewport: {startIndex}-{endIndex}");
            }
            
            // Final safety checks to ensure we never exceed totalRows
            _visibleStartIndex = Math.Max(0, Math.Min(startIndex, totalRows));
            _visibleEndIndex = Math.Max(_visibleStartIndex, Math.Min(endIndex, totalRows));
            
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
        /// Updates the cell count for the last render
        /// </summary>
        /// <param name="cellCount">Number of cells that were drawn</param>
        public void UpdateCellCount(int cellCount)
        {
            CellsDrawn = cellCount;
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
            CellsDrawn = 0;
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