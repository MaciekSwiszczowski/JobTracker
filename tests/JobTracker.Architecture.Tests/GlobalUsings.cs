// Global using directives

global using System.ComponentModel;
global using System.Runtime.CompilerServices;
global using ArchUnitNET.Domain;
global using ArchUnitNET.xUnitV3;
global using JobTracker.TestSupport;
global using Shouldly;
global using Xunit;
global using static ArchUnitNET.Fluent.ArchRuleDefinition;

// ArchUnitNET.Domain has its own Assembly type; this project only ever means the reflection one.
global using Assembly = System.Reflection.Assembly;
