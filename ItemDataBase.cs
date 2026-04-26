namespace IsaacPickupScanner;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class ItemDatabase
{
    private readonly Dictionary<int, List<Item>> _items;
    private readonly Dictionary<int, List<Item>> _trinkets;
    private readonly Dictionary<int, List<Item>> _cards;

    private readonly Dictionary<int, Dictionary<int, List<Item>>> _typeMap;

    public ItemDatabase(string jsonPath)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var json = File.ReadAllText(jsonPath);

        var list = JsonSerializer.Deserialize<List<Item>>(json, options)
                   ?? new List<Item>();

        _items = list
            .Where(x => x.Category == "item")
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        _trinkets = list
            .Where(x => x.Category == "trinket")
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        _cards = list
            .Where(x => x.Category == "card")
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        _typeMap = new()
        {
            { 100, _items },
            { 350, _trinkets },
            { 300, _cards }
        };
    }

    public List<Item>? GetItems(int id, int type)
    {
        if (_typeMap.TryGetValue(type, out var dict))
        {
            return dict.TryGetValue(id, out var items) ? items : null;
        }

        return null;
    }
}

class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Pickup { get; set; }
    public int Quality { get; set; }
    public List<string> Description { get; set; }
    public string Type { get; set; }
    public string Pools { get; set; }
    public string Tags { get; set; }
    public string Category { get; set; }
}