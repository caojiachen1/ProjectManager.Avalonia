using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.Behaviors;

/// <summary>
/// Provides drag-drop reordering inside an <see cref="ItemsControl"/>.
/// <para>Usage:</para>
/// <list type="bullet">
///   <item>Set <c>DragDropReorder.Enable="True"</c> on the <see cref="ItemsControl"/>.</item>
///   <item>Set <c>DragDropReorder.IsDragHandle="True"</c> on the drag-handle element inside the item template.</item>
/// </list>
/// </summary>
public static class DragDropReorder
{
    private const double DragThreshold = 4.0;

    /// <summary>
    /// Typed data format for in-process drag of reorder items.
    /// </summary>
    private static readonly DataFormat<object> ReorderFormat =
        DataFormat.CreateInProcessFormat<object>("ProjectManager.ReorderItem");

    // ──────────────────────────────────────────────
    //  Attached Properties
    // ──────────────────────────────────────────────

    /// <summary>
    /// Enables drag-drop reordering on an <see cref="ItemsControl"/>.
    /// </summary>
    public static readonly AttachedProperty<bool> EnableProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>(
            "Enable", typeof(DragDropReorder), false);

    /// <summary>
    /// Marks an element as a drag handle. Only pointer presses that originate
    /// within a drag handle (or its descendants) will initiate a drag.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDragHandleProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, bool>(
            "IsDragHandle", typeof(DragDropReorder), false);

    public static bool GetEnable(ItemsControl control) => control.GetValue(EnableProperty);
    public static void SetEnable(ItemsControl control, bool value) => control.SetValue(EnableProperty, value);

    public static bool GetIsDragHandle(AvaloniaObject obj) => obj.GetValue(IsDragHandleProperty);
    public static void SetIsDragHandle(AvaloniaObject obj, bool value) => obj.SetValue(IsDragHandleProperty, value);

    // ──────────────────────────────────────────────
    //  Per-control drag state
    // ──────────────────────────────────────────────

    private sealed class DragState
    {
        public Point StartPoint;
        public object? DraggedItem;
        public bool IsPointerDown;
        public Control? DraggedContainer;
        public Control? HighlightedContainer;
        public double DraggedOriginalOpacity;
        public double HighlightOriginalOpacity;
        public PointerPressedEventArgs? PressEventArgs;
    }

    // ConditionalWeakTable avoids preventing GC of the ItemsControl.
    private static readonly ConditionalWeakTable<ItemsControl, DragState> States = new();

    private static DragState GetState(ItemsControl control) =>
        States.GetValue(control, _ => new DragState());

    // ──────────────────────────────────────────────
    //  Static constructor – wire up property-changed handler
    // ──────────────────────────────────────────────

    static DragDropReorder()
    {
        EnableProperty.Changed.AddClassHandler<ItemsControl>(OnEnableChanged);
    }

    // ──────────────────────────────────────────────
    //  Enable / Disable event subscriptions
    // ──────────────────────────────────────────────

    private static void OnEnableChanged(ItemsControl ic, AvaloniaPropertyChangedEventArgs e)
    {
        bool enabled = e.NewValue is true;

        if (enabled)
        {
            DragDrop.SetAllowDrop(ic, true);

            // Tunnel so we see the press before child controls can mark it handled.
            ic.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            ic.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
            ic.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);

            ic.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            ic.AddHandler(DragDrop.DropEvent, OnDrop);
            ic.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }
        else
        {
            ic.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            ic.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            ic.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);

            ic.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            ic.RemoveHandler(DragDrop.DropEvent, OnDrop);
            ic.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }
    }

    // ──────────────────────────────────────────────
    //  Pointer event handlers (drag initiation)
    // ──────────────────────────────────────────────

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ItemsControl ic) return;

        var source = e.Source as Visual;
        if (source == null || !IsFromHandle(source)) return;

        var state = GetState(ic);

        // Reset stale state from a previous incomplete interaction.
        if (state.IsPointerDown)
        {
            ResetDragVisualFeedback(state);
            state.IsPointerDown = false;
            state.DraggedItem = null;
            state.DraggedContainer = null;
        }

        state.StartPoint = e.GetPosition(ic);
        state.IsPointerDown = true;
        state.PressEventArgs = e;

        var container = FindItemContainer(ic, source);
        state.DraggedContainer = container;
        state.DraggedItem = container != null ? GetItemFromContainer(ic, container) : null;
    }

    private static async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ItemsControl ic) return;
        var state = GetState(ic);

        if (!state.IsPointerDown || state.DraggedItem == null || state.PressEventArgs == null)
            return;

        var point = e.GetCurrentPoint(ic);
        if (!point.Properties.IsLeftButtonPressed)
        {
            // Button released without exceeding drag threshold – reset.
            state.IsPointerDown = false;
            state.DraggedItem = null;
            state.DraggedContainer = null;
            state.PressEventArgs = null;
            return;
        }

        var currentPos = e.GetPosition(ic);
        if (Math.Abs(currentPos.X - state.StartPoint.X) < DragThreshold &&
            Math.Abs(currentPos.Y - state.StartPoint.Y) < DragThreshold)
            return;

        try
        {
            // Dim the dragged container for visual feedback.
            if (state.DraggedContainer != null)
            {
                state.DraggedOriginalOpacity = state.DraggedContainer.Opacity;
                state.DraggedContainer.Opacity = 0.5;
            }

            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(ReorderFormat, state.DraggedItem));

            // DoDragDropAsync needs the original PointerPressedEventArgs.
            await DragDrop.DoDragDropAsync(state.PressEventArgs, dataTransfer, DragDropEffects.Move);
        }
        catch
        {
            // Swallow errors during drag to avoid crashing the UI.
        }
        finally
        {
            ResetDragVisualFeedback(state);
            state.IsPointerDown = false;
            state.DraggedItem = null;
            state.DraggedContainer = null;
            state.PressEventArgs = null;
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ItemsControl ic) return;
        var state = GetState(ic);
        state.IsPointerDown = false;
        state.DraggedItem = null;
        state.DraggedContainer = null;
        state.PressEventArgs = null;
    }

    // ──────────────────────────────────────────────
    //  Drag-over / Drop / Drag-leave handlers
    // ──────────────────────────────────────────────

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl ic) return;

        if (!e.DataTransfer.Contains(ReorderFormat))
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        // Highlight the item under the pointer as the drop target.
        var state = GetState(ic);
        var pos = e.GetPosition(ic);
        var hitElement = ic.InputHitTest(pos) as Visual;
        var targetContainer = FindItemContainer(ic, hitElement);

        if (targetContainer != state.HighlightedContainer)
        {
            // Remove previous highlight.
            if (state.HighlightedContainer != null)
                state.HighlightedContainer.Opacity = state.HighlightOriginalOpacity;

            // Apply new highlight (skip if it is the dragged item itself).
            if (targetContainer != null && targetContainer != state.DraggedContainer)
            {
                state.HighlightOriginalOpacity = targetContainer.Opacity;
                targetContainer.Opacity = 0.8;
                state.HighlightedContainer = targetContainer;
            }
            else
            {
                state.HighlightedContainer = null;
            }
        }
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl ic) return;
        if (!e.DataTransfer.Contains(ReorderFormat)) return;

        var draggedItem = e.DataTransfer.TryGetValue(ReorderFormat);
        if (draggedItem == null) return;

        // Determine the target item under the pointer.
        var pos = e.GetPosition(ic);
        var hitElement = ic.InputHitTest(pos) as Visual;
        var targetContainer = FindItemContainer(ic, hitElement);
        var targetItem = targetContainer != null ? GetItemFromContainer(ic, targetContainer) : null;

        var list = GetItemsList(ic);
        if (list == null) return;

        var oldIndex = list.IndexOf(draggedItem);
        if (oldIndex < 0) return;

        int newIndex;
        if (targetItem == null || ReferenceEquals(targetItem, draggedItem))
        {
            // Dropped on empty space or on itself → move to end.
            newIndex = list.Count - 1;
        }
        else
        {
            newIndex = list.IndexOf(targetItem);
        }

        if (newIndex < 0 || newIndex == oldIndex) return;

        MoveItem(list, oldIndex, newIndex);

        // Attempt to persist the new order via DI.
        TrySaveOrder(ic);

        // Reset visual feedback.
        var state = GetState(ic);
        ResetDragVisualFeedback(state);

        e.Handled = true;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl ic) return;
        var state = GetState(ic);

        if (state.HighlightedContainer != null)
        {
            state.HighlightedContainer.Opacity = state.HighlightOriginalOpacity;
            state.HighlightedContainer = null;
        }
    }

    // ──────────────────────────────────────────────
    //  Helper methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Walks up the visual tree to determine whether the pointer press
    /// originated inside an element marked as a drag handle.
    /// </summary>
    private static bool IsFromHandle(Visual? source)
    {
        AvaloniaObject? current = source;
        while (current != null)
        {
            if (current.GetValue(IsDragHandleProperty))
                return true;
            current = (current as Visual)?.Parent;
        }
        return false;
    }

    /// <summary>
    /// Walks up the visual tree from <paramref name="source"/> looking for a
    /// <see cref="Control"/> that is a direct item container of the given
    /// <see cref="ItemsControl"/> (i.e. <see cref="ItemsControl.IndexFromContainer"/>
    /// returns a non-negative index).
    /// </summary>
    private static Control? FindItemContainer(ItemsControl ic, Visual? source)
    {
        Visual? current = source;
        while (current != null && current != ic)
        {
            if (current is Control control)
            {
                int index = ic.IndexFromContainer(control);
                if (index >= 0)
                    return control;
            }
            current = current.Parent as Visual;
        }
        return null;
    }

    private static object? GetItemFromContainer(ItemsControl ic, Control container)
    {
        int index = ic.IndexFromContainer(container);
        if (index < 0) return null;

        var list = GetItemsList(ic);
        if (list != null && index < list.Count)
            return list[index];

        return null;
    }

    private static IList? GetItemsList(ItemsControl ic)
    {
        if (ic.ItemsSource is IList list)
            return list;
        return ic.Items as IList;
    }

    /// <summary>
    /// Moves an item within the list. Prefers <c>ObservableCollection&lt;T&gt;.Move</c>
    /// via reflection (which fires a single <c>Move</c> notification); falls back to
    /// Remove + Insert for plain lists.
    /// </summary>
    private static void MoveItem(IList list, int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex) return;

        // Try ObservableCollection<T>.Move(oldIndex, newIndex) via reflection.
        var moveMethod = list.GetType().GetMethod("Move", [typeof(int), typeof(int)]);
        if (moveMethod != null)
        {
            moveMethod.Invoke(list, [oldIndex, newIndex]);
            return;
        }

        // Fallback: RemoveAt + Insert.
        if (list.IsReadOnly) return;

        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        if (newIndex >= list.Count)
            list.Add(item);
        else
            list.Insert(newIndex, item);
    }

    private static void ResetDragVisualFeedback(DragState state)
    {
        if (state.DraggedContainer != null)
            state.DraggedContainer.Opacity = state.DraggedOriginalOpacity;

        if (state.HighlightedContainer != null)
        {
            state.HighlightedContainer.Opacity = state.HighlightOriginalOpacity;
            state.HighlightedContainer = null;
        }
    }

    /// <summary>
    /// Attempts to persist the reordered project list via
    /// <see cref="IProjectService.SaveProjectsOrderAsync"/> obtained from the
    /// application DI container (<see cref="App.Services"/>).
    /// </summary>
    private static void TrySaveOrder(ItemsControl ic)
    {
        try
        {
            var svc = App.Services.GetService(typeof(IProjectService)) as IProjectService;
            if (svc == null) return;

            if (ic.ItemsSource is IEnumerable enumerable)
            {
                var ordered = new List<Project>();
                foreach (var item in enumerable)
                {
                    if (item is Project p)
                        ordered.Add(p);
                }

                // Fire-and-forget – persistence errors must not disrupt the UI.
                _ = svc.SaveProjectsOrderAsync(ordered);
            }
        }
        catch
        {
            // Ignore persistence errors.
        }
    }
}
