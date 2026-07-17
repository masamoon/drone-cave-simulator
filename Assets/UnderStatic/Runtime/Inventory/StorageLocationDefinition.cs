using System;
using System.Collections.Generic;
using UnderStatic.Core;
using UnderStatic.Parts;
using UnityEngine;

namespace UnderStatic.Inventory
{
    public enum StorageLocationKind
    {
        Parts,
        Returns,
        Salvage
    }

    [CreateAssetMenu(menuName = "Under Static/Storage Location Definition", fileName = "StorageLocationDefinition")]
    public sealed class StorageLocationDefinition : ScriptableObject
    {
        [SerializeField] private string id = "safehouse.parts";
        [SerializeField] private string displayName = "Parts Storage";
        [SerializeField] private StorageLocationKind kind = StorageLocationKind.Parts;
        [SerializeField] private PartCategory[] acceptedCategories = Array.Empty<PartCategory>();
        [SerializeField, Min(1)] private int capacity = 4;

        public StorageLocationId Id => new(id);
        public string DisplayName => displayName;
        public StorageLocationKind Kind => kind;
        public IReadOnlyList<PartCategory> AcceptedCategories => acceptedCategories;
        public int Capacity => Mathf.Max(1, capacity);

        public bool Accepts(InstallablePart part)
        {
            if (part == null || part.Runtime.isSalvaged || part.Definition == null)
            {
                return false;
            }

            if (acceptedCategories != null
                && acceptedCategories.Length > 0
                && Array.IndexOf(acceptedCategories, part.Definition.Category) < 0)
            {
                return false;
            }

            return kind switch
            {
                StorageLocationKind.Parts => part.IsServiceable && !part.IsBatteryDepleted,
                StorageLocationKind.Returns => !part.IsServiceable || part.IsBatteryDepleted,
                StorageLocationKind.Salvage => !part.IsServiceable,
                _ => false
            };
        }

        public static StorageLocationDefinition CreateTransient(
            string locationId,
            string name,
            StorageLocationKind locationKind,
            int slotCapacity,
            params PartCategory[] categories)
        {
            var definition = CreateInstance<StorageLocationDefinition>();
            definition.id = locationId ?? string.Empty;
            definition.displayName = name ?? locationId ?? "Storage";
            definition.kind = locationKind;
            definition.capacity = Mathf.Max(1, slotCapacity);
            definition.acceptedCategories = categories ?? Array.Empty<PartCategory>();
            return definition;
        }
    }
}
