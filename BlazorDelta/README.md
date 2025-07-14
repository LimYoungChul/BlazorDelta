# BlazorDelta

**High-performance Blazor components with source generation**

[![NuGet](https://img.shields.io/nuget/v/BlazorDelta.svg)](https://www.nuget.org/packages/BlazorDelta/)
[![Downloads](https://img.shields.io/nuget/dt/BlazorDelta.svg)](https://www.nuget.org/packages/BlazorDelta/)

## ⚡ Performance Improvements

- **3-4x faster** parameter assignment than ComponentBase
- **Near-zero memory allocation** (464B → 0B for attribute splatting)
- **Automatic CSS optimization** with smart change detection
- **Parameter change handlers** without reflection overhead

## 🚀 Benchmark Results

| Scenario | ComponentBase | BlazorDelta | Speedup |
|----------|---------------|-----------------|---------|
| Simple Parameters | 36.9 ns | 8.6 ns | **4.3x** |
| Complex Parameters | 75.7 ns | 21.1 ns | **3.6x** |
| Incremental Updates | 68.5 ns | 16.4 ns | **4.2x** |
| Unmatched Attributes | 136.1 ns | 40.2 ns | **3.4x** |

## 📦 Installation

```bash
dotnet add package BlazorDelta
```

## Usage

1. Create a component that inherits from `DeltaBaseComponent`:

```csharp
public partial class MyComponent : DeltaBaseComponent
{
    [Parameter] public string Title { get; set; }
    [Parameter] public int Count { get; set; }
    
    // Your component logic here
}
```

2. Build your project - the source generator will automatically create optimized partial methods for your component.

3. Your component now has enhanced delta functionality with automatically generated methods!

## How It Works

BlazorDelta uses source generators to analyze your components that inherit from `DeltaBaseComponent` and automatically generates:

- Optimized state change detection methods
- Delta comparison utilities  
- Performance-enhanced rendering logic

All generated code is created as partial class extensions, so your original component code remains clean and focused.

## Features

- ✅ Automatic source generation at compile time
- ✅ Performance optimizations for Blazor components
- ✅ Seamless integration with existing Blazor projects
- ✅ Type-safe generated code
- ✅ Zero runtime dependencies for the generator
- ✅ Full IntelliSense support for generated methods

## Requirements

- .NET 9.0 or later
- Blazor Server or Blazor WebAssembly project

## Example

```csharp
// Your component
public partial class CounterComponent : DeltaBaseComponent
{
    [Parameter] public int CurrentCount { get; set; }
    [Parameter] public string Label { get; set; } = "Counter";

    private void IncrementCount()
    {
        CurrentCount++;
        // Generated delta methods automatically handle optimized updates
    }
}
```

The source generator will create additional methods and optimizations automatically when you build.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This project is licensed under the MIT License.