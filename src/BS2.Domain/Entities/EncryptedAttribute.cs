namespace BS2.Domain.Entities;

/// <summary>
/// Marks a string property for application-level encryption at rest. Infrastructure applies a
/// value converter to every property carrying it, so the entities stay persistence-agnostic.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;
