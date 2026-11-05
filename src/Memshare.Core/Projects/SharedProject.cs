using Memshare.Stores;

namespace Memshare.Projects;

/// <summary>A project with shared notes in a store.</summary>
/// <param name="Store">The store.</param>
/// <param name="Slug">The project's name in the store.</param>
/// <param name="LastPublished">When its notes were last published; null if never.</param>
public sealed record SharedProject(Store Store, string Slug, DateTimeOffset? LastPublished);
