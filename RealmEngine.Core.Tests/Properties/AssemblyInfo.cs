using System.Diagnostics.CodeAnalysis;

// Exclude the entire test assembly from code-coverage reporting.
// Test code itself is never a coverage target — this prevents the VS Code
// coverage viewer from showing test files as "0% covered".
[assembly: ExcludeFromCodeCoverage]
