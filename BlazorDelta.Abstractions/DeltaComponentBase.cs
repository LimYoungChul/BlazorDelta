using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorDelta.Abstractions;

/// <summary>
/// Base component that uses source generation for high-performance parameter setting
/// instead of reflection-based parameter assignment.
/// </summary>
public abstract class DeltaComponentBase : IComponent, IHandleEvent, IHandleAfterRender
{
    private readonly RenderFragment _renderFragment;
    private (IComponentRenderMode? mode, bool cached) _renderMode;
    private readonly Queue<Func<Task>> _afterRenderQueue = new();
    private RenderHandle _renderHandle;
    private bool _hasNeverRendered = true;
    private bool _hasPendingQueuedRender;
    private bool _hasCalledOnAfterRender;

    /// <summary>
    /// When true, CSS classes will be updated on the next render.
    /// Set this to true manually when CSS needs to update due to non-parameter changes.
    /// </summary>
    protected bool DirtyCss { get; set; }

    /// <summary>
    /// Constructs an instance of <see cref="DeltaComponentBase"/>.
    /// </summary>
    protected DeltaComponentBase()
    {
        _renderFragment = builder =>
        {
            _hasPendingQueuedRender = false;
            _hasNeverRendered = false;
            BuildRenderTree(builder);
        };
    }

    /// <summary>
    /// Gets the <see cref="Components.RendererInfo"/> the component is running on.
    /// </summary>
    protected RendererInfo RendererInfo => _renderHandle.RendererInfo;

    /// <summary>
    /// Gets the <see cref="ResourceAssetCollection"/> for the application.
    /// </summary>
    protected ResourceAssetCollection Assets => _renderHandle.Assets;

    /// <summary>
    /// Gets the <see cref="IComponentRenderMode"/> assigned to this component.
    /// </summary>
    protected IComponentRenderMode? AssignedRenderMode
    {
        get
        {
            if (!_renderMode.cached)
            {
                _renderMode = (_renderHandle.RenderMode, true);
            }

            return _renderMode.mode;
        }
    }


    /// <summary>
    /// Renders the component to the supplied <see cref="RenderTreeBuilder"/>.
    /// </summary>
    /// <param name="builder">A <see cref="RenderTreeBuilder"/> that will receive the render output.</param>
    protected virtual void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Developers can either override this method in derived classes, or can use Razor
        // syntax to define a derived class and have the compiler generate the method.

        // Other code within this class should *not* invoke BuildRenderTree directly,
        // but instead should invoke the _renderFragment field.
    }

    /// <summary>
    /// Method invoked when the component is ready to start, having received its
    /// initial parameters from its parent in the render tree.
    /// </summary>
    protected virtual void OnInitialized()
    {
        UpdateCssClasses();
    }

    /// <summary>
    /// Method invoked when the component is ready to start, having received its
    /// initial parameters from its parent in the render tree.
    /// Override this method if you will perform an asynchronous operation and
    /// want the component to refresh when that operation is completed.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation.</returns>
    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in
    /// the render tree, and the incoming values have been assigned to properties.
    /// </summary>
    protected virtual void OnParametersSet()
    {
    }

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in
    /// the render tree, and the incoming values have been assigned to properties.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation.</returns>
    protected virtual Task OnParametersSetAsync() => Task.CompletedTask;

    /// <summary>
    /// Notifies the component that its state has changed. When applicable, this will
    /// cause the component to be re-rendered.g
    /// </summary>
    protected void StateHasChanged()
    {
        if (_hasPendingQueuedRender)
            return;

        if (_hasNeverRendered || ShouldRender())
        {
            _hasPendingQueuedRender = true;

            // Update CSS if dirty
            if (DirtyCss)
            {
                UpdateCssClasses();
                DirtyCss = false;
            }

            try
            {
                _renderHandle.Render(_renderFragment);
            }
            catch
            {
                _hasPendingQueuedRender = false;
                throw;
            }
        }
    }

    /// <summary>
    /// Returns a flag to indicate whether the component should render.
    /// </summary>
    /// <returns>True if the component should render, otherwise false.</returns>
    protected virtual bool ShouldRender() => true;

    /// <summary>
    /// Method invoked after each time the component has been rendered.
    /// </summary>
    /// <param name="firstRender">
    /// Set to <c>true</c> if this is the first time <see cref="OnAfterRender(bool)"/> has been invoked
    /// on this component instance; otherwise <c>false</c>.
    /// </param>
    protected virtual void OnAfterRender(bool firstRender)
    {
    }

    /// <summary>
    /// Method invoked after each time the component has been rendered. Note that the component does
    /// not automatically re-render after the completion of any returned <see cref="Task"/>, because
    /// that would cause an infinite render loop.
    /// </summary>
    /// <param name="firstRender">
    /// Set to <c>true</c> if this is the first time <see cref="OnAfterRenderAsync(bool)"/> has been invoked
    /// on this component instance; otherwise <c>false</c>.
    /// </param>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation.</returns>
    protected virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;

    /// <summary>
    /// Override this method to update CSS classes when parameters marked with [UpdatesCss] change
    /// or when DirtyCss is set to true manually.
    /// </summary>
    protected virtual void UpdateCssClasses()
    {
    }

    /// <summary>
    /// Queues an action to be executed after the next render.
    /// Useful for DOM operations or other post-render tasks.
    /// </summary>
    /// <param name="action">The action to execute after render.</param>
    protected void QueueAfterRender(Action action)
    {
        _afterRenderQueue.Enqueue(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Queues an async action to be executed after the next render.
    /// Useful for DOM operations or other post-render tasks.
    /// </summary>
    /// <param name="asyncAction">The async action to execute after render.</param>
    protected void QueueAfterRender(Func<Task> asyncAction)
    {
        _afterRenderQueue.Enqueue(asyncAction);
    }

    /// <summary>
    /// Sets parameters supplied by the component's parent in the render tree.
    /// This method is overridden by the source generator to provide high-performance parameter assignment.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A <see cref="Task"/> that completes when the component has finished updating and rendering itself.</returns>
    public virtual async Task SetParametersAsync(ParameterView parameters)
    {
        // Call the source-generated parameter setting method
        bool shouldRender = SetParametersFromSource(parameters);

        if (_hasNeverRendered)
        {
            // This is the first time SetParametersAsync has been called on this component.
            _hasNeverRendered = false;
            OnInitialized();
            var task = OnInitializedAsync();

            if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
            {
                StateHasChanged();
                try
                {
                    await task;
                }
                catch
                {
                    if (!_hasCalledOnAfterRender)
                    {
                        StateHasChanged();
                    }
                    throw;
                }
            }
        }

        OnParametersSet();
        var onParametersSetTask = OnParametersSetAsync();

        shouldRender = shouldRender && ShouldRender();
        if (shouldRender || _hasNeverRendered)
        {
            StateHasChanged();
        }

        if (onParametersSetTask.Status != TaskStatus.RanToCompletion && onParametersSetTask.Status != TaskStatus.Canceled)
        {
            try
            {
                await onParametersSetTask;
            }
            catch
            {
                if (!_hasCalledOnAfterRender)
                {
                    StateHasChanged();
                }
                throw;
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// This method is overridden by the source generator to provide high-performance parameter assignment.
    /// Do not call this method directly.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    public virtual bool SetParametersFromSource(ParameterView parameters)
    {
        // This will be overridden by the source generator
        // Fallback to reflection-based assignment for components without source generation
        parameters.SetParameterProperties(this);
        return true;
    }

    void IComponent.Attach(RenderHandle renderHandle)
    {
        if (_renderHandle.IsInitialized)
        {
            throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(DeltaComponentBase)} more than once.");
        }

        _renderHandle = renderHandle;
    }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = callback.InvokeAsync(arg);
        var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
                              task.Status != TaskStatus.Canceled;

        StateHasChanged();

        return shouldAwaitTask ?
            CallStateHasChangedOnAsyncCompletion(task) :
            Task.CompletedTask;
    }

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !_hasCalledOnAfterRender;
        _hasCalledOnAfterRender = true;

        OnAfterRender(firstRender);

        return ProcessAfterRenderQueue(firstRender);
    }
  
    private async Task ProcessAfterRenderQueue(bool firstRender)
    {
        // Execute user's OnAfterRenderAsync first
        await OnAfterRenderAsync(firstRender);

        // Process all queued actions sequentially
        while (_afterRenderQueue.Count > 0)
        {
            var action = _afterRenderQueue.Dequeue();
            try
            {
                await action.Invoke();
            }
            catch
            {
                
            }
        }
    }

    private async Task CallStateHasChangedOnAsyncCompletion(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            if (task.IsCanceled)
            {
                return;
            }
            throw;
        }

        StateHasChanged();
    }
}