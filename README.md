# Brave

[![NuGet](https://img.shields.io/nuget/v/Brave.svg)](https://www.nuget.org/packages/Brave)
[![Sample Gallery](https://img.shields.io/badge/Sample%20Gallery-live%20demo-blue)](https://2xpro-pop.github.io/Brave/)
[![Donate USDT](https://img.shields.io/badge/Donate-USDT%20(TRC--20)-green)](https://tronscan.org/#/address/TTMutDKfCS6NCPY3Q2AU3Tgxb9jnzmJm6q)

> If it's possible and easy to do in XAML - do it in XAML.

**Brave** is a tiny library that uses a **ResourceDictionary as a state holder** and adds a small expression language for simple UI logic.
It is **not** an MVVM framework and **not** a general-purpose state-management solution.
Brave is meant for **small UI state** and **simple interactions**, written directly in XAML.

## Table of Contents

- [What Brave is](#what-brave-is)
- [What Brave is not](#what-brave-is-not)
- [Installation](#installation)
- [How it works (high level)](#how-it-works-high-level)
- [Usage](#usage)
- [Expression language (supported operations)](#expression-language-supported-operations)
- [Notes](#notes)
- [Roadmap / TODO](#roadmap--todo)
- [Support](#support)

## What Brave is

- A lightweight way to store UI state in resources (e.g. `$IsLoading`, `$Counter`, `$ErrorText`)
- A compact expression language for small UI actions and derived values
- A good fit for:
  - visibility / enable-disable toggles
  - simple counters and flags
  - UI-only state (loading, error, selected item key, etc.)
  - small computed values for display

## What Brave is not

- Not a replacement for **Bindings**, **ReactiveUI**, MVVM, or proper domain/business logic
- Not intended for complex logic or large apps where state should be structured and testable outside the view

## Installation

NuGet:

```bash
dotnet add package Brave
````

## How it works (high level)

* `brave:InitResources` executes once (during XAML load) and writes values directly into the resource dictionary.
* `brave:Execute` executes a compiled instruction list on demand (e.g. `Button.Command`).
* Resources can be consumed via:

  * `DynamicResource` (best for simple “take value by key” cases)
  * `brave:RBinding` (for computed expressions and convenient string/number proxying)

## Usage

### 1) Counter example

```xaml
<Window xmlns="https://github.com/avaloniaui"
		xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
		xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
		xmlns:brave="http://github.com/2Xpro-pop/Brave"

		x:Class="Counter.MainWindow"
		Title="Counter">

	<Button Resources="{brave:InitResources '$Counter=20 * 5 ^ 17 - 3; $Text=\'Counter: \' + $Counter '}"
	        Command="{brave:Execute Expression='$Counter++; $Text=\'Counter:\' + $Counter'}">
		<TextBlock Text="{DynamicResource $Text}"/>
	</Button>

</Window>
```

### 2) Editing numbers and showing computed output with `RBinding`

If you store `$A` and `$B` as numbers, you often want a nice `proxy` for editing and computed UI text.
Use `brave:RBinding`:

```xaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:brave="http://github.com/2Xpro-pop/Brave"
        x:Class="Adder.MainWindow"
        Title="Adder">

  <StackPanel Spacing="8"
              Resources="{brave:InitResources '$A = 2d; $B = 11d'}">

    <TextBox Text="{brave:RBinding $A}" />
    <TextBox Text="{brave:RBinding $B}" />

    <TextBlock Text="{brave:RBinding '$A + $B'}" />
  </StackPanel>

</Window>
```

More examples: **[Sample Gallery](https://2xpro-pop.github.io/Brave/)** (live demo) and **Samples** folder in the repository.

## Expression language (supported operations)

Brave expressions are intentionally small and focused.

### Literals

* Numbers (decimal / hex / binary) with suffixes: `1`, `1d`, `1f`, `1m`, `0xFF`, `0b1010`
* Strings: `'text'`, `"text"`, verbatim: `@"text"`, `@'text'`
* Booleans: `true`, `false`
* `null`

### Variables / special values

* Resource variables: `$MyKey`
* `$parameter` - command parameter
* `$self` - owner object (control instance)

### Assignment

* `$A = 10`
* Multiple statements: `$A = 1; $B = $A + 2`
* Compound: `$A += 1`, `$A -= 2`, `$A *= 3`, `$A /= 4`
* Null-coalescing assignment: `$A ??= 'default'`

### Indexing

* Read: `$List[0]`, `$Dict[$Key]`
* Write: `$List[0] = 42`
* Compound index assignment: `$List[0] += 1`

### Invocation

* `$Action()` - invoke a resource as a command
* `$Action($param)` - invoke with a parameter expression

### Arithmetic

* `+  -  *  /`
* Unary negate: `-value`

### Comparison

* `==  !=  <  <=  >  >=`

### Logical

* `!value`
* `&&` and `||` with **short-circuit** via jumps

### Null-coalescing and ternary

* `a ?? b`
* `cond ? then : else`

### Bitwise

* `~`
* `&  |  ^`

### Increment / decrement

* `++$A`, `$A++`
* `--$A`, `$A--`

## Notes

* Brave is designed for **UI logic**. Keep business logic outside XAML.
* The runtime stack is intentionally small and pooled to reduce allocations.
* Expressions are compiled into a high-level instruction list (cached), while resource tables are per-instance.

## Roadmap / TODO

- [ ] Binding to ViewModel properties
- [ ] Dot-access for nested objects (`$Hello.$World`)
- [ ] Binding to `ObservableCollection`
- [ ] Binding to `IObservable`
- [ ] Improve resource change notification performance
- [ ] Better error handling and diagnostics (e.g. invalid expressions, missing keys, type errors)
- [ ] Support for more platforms (e.g. MAUI, WPF, etc.)
- [ ] new keyword for creating objects
- [ ] Aliases for >, <, etc. (e.g. `gt`, `lt`) instead of writing &amp;gt; in XAML
- [ ] Pipe operator for chaining operations (e.g. `$A |# sqrt |# round`)

## Support

If you find Brave useful and want to support its development, you can send a donation:

**USDT (TRC-20)**

```
TTMutDKfCS6NCPY3Q2AU3Tgxb9jnzmJm6q
```

