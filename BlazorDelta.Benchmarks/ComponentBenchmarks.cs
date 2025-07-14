using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using BlazorDelta.Abstractions;

namespace BlazorDelta.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class ComponentBenchmark
{
    private ParameterView _simpleParameterView;
    private ParameterView _complexParameterView;
    private ParameterView _unchangedParameterView;
    private ParameterView _eventCallbackParameterView;
    private ParameterView _unmatchedParameterView;
    private ParameterView _initialParameterView;
    private ParameterView _incrementalUpdateView;

    private StandardComponent _standardComponent = null!;
    private GeneratedComponent _generatedComponent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _standardComponent = new StandardComponent();
        _generatedComponent = new GeneratedComponent();

        // Simple parameter changes
        _simpleParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "Test Title" },
            { "Count", 42 }
        });


        // Complex parameter changes
        _complexParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "Complex Title" },
            { "Count", 100 },
            { "IsVisible", true },
            { "Theme", "dark" },
            { "Items", new List<string> { "Item1", "Item2", "Item3" } },
            { "OnClick", EventCallback.Factory.Create(this, () => { }) }
        });

        // No actual changes (same values as defaults)
        _unchangedParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "" },
            { "Count", 0 },
            { "IsVisible", false }
        });

        // EventCallback parameter
        _eventCallbackParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "OnClick", EventCallback.Factory.Create(this, () => { }) }
        });

        // CaptureUnmatchedValues test (attributes that don't match known parameters)
        _unmatchedParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "Test" },  // Known parameter
            { "class", "btn btn-primary" },  // Unmatched - goes to AdditionalAttributes
            { "data-id", "123" },  // Unmatched - goes to AdditionalAttributes
            { "style", "color: red;" },  // Unmatched - goes to AdditionalAttributes
            { "aria-label", "Test button" }  // Unmatched - goes to AdditionalAttributes
        });

        // Incremental update test - realistic scenario
        _initialParameterView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "Initial Title" },
            { "Count", 100 },
            { "IsVisible", true },
            { "Theme", "dark" },
            { "Items", new List<string> { "Item1", "Item2" } }
        });

        _incrementalUpdateView = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            { "Title", "Updated Title" },  // Changed
            { "Count", 150 },              // Changed  
            { "IsVisible", true },         // Unchanged
            { "Theme", "dark" },           // Unchanged
            { "Items", new List<string> { "Item1", "Item2" } }  // Unchanged (same reference)
        });


        // Set up initial state for incremental update test
        _initialParameterView.SetParameterProperties(_standardComponent);
        _generatedComponent.SetParametersFromSource(_initialParameterView);

    }

    [Benchmark(Baseline = true)]
    public void Blazor_SetParameterProperties_Simple()
    {
        // This is exactly what ComponentBase.SetParametersAsync() calls internally
        _simpleParameterView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_Simple()
    {
        _generatedComponent.SetParametersFromSource(_simpleParameterView);
    }

    [Benchmark]
    public void Blazor_SetParameterProperties_Complex()
    {
        _complexParameterView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_Complex()
    {
        _generatedComponent.SetParametersFromSource(_complexParameterView);
    }

    [Benchmark]
    public void Blazor_SetParameterProperties_Unchanged()
    {
        _unchangedParameterView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_Unchanged()
    {
        _generatedComponent.SetParametersFromSource(_unchangedParameterView);
    }

    [Benchmark]
    public void Blazor_SetParameterProperties_EventCallback()
    {
        _eventCallbackParameterView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_EventCallback()
    {
        _generatedComponent.SetParametersFromSource(_eventCallbackParameterView);
    }

    [Benchmark]
    public void Blazor_SetParameterProperties_Unmatched()
    {
        _unmatchedParameterView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_Unmatched()
    {
        _generatedComponent.SetParametersFromSource(_unmatchedParameterView);
    }

    [Benchmark]
    public void Blazor_SetParameterProperties_IncrementalUpdate()
    {
        // Measure just the incremental update (2 of 5 parameters changed)
        _incrementalUpdateView.SetParameterProperties(_standardComponent);
    }

    [Benchmark]
    public void Generated_SetParametersFromSource_IncrementalUpdate()
    {
        // Measure just the incremental update (2 of 5 parameters changed)
        _generatedComponent.SetParametersFromSource(_incrementalUpdateView);
    }
}

// Standard component using Blazor's reflection-based parameter setting
public class StandardComponent
{
    [Parameter] public string? Title { get; set; } = "";
    [Parameter] public int Count { get; set; } = 0;
    [Parameter] public bool IsVisible { get; set; } = false;
    [Parameter] public string? Theme { get; set; } = "";
    [Parameter] public List<string>? Items { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}

// Your optimized component using source generation (PURE VERSION - no extra features)
public partial class GeneratedComponent : DeltaComponentBase
{
    [Parameter] public string? Title { get; set; } = "";
    [Parameter] public int Count { get; set; } = 0;
    [Parameter] public bool IsVisible { get; set; } = false;
    [Parameter] public string? Theme { get; set; } = "";  // Removed UpdatesCss for pure test
    [Parameter] public List<string>? Items { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // Removed HandleCountChanged method for pure parameter assignment test
    // Removed UpdateCssClasses override for pure test

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // Minimal implementation for source generation to work
    }
}