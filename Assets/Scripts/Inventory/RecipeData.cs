using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Recipe Data", menuName = "Culinaria/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [SerializeField]private string recipeName;
    [SerializeField]private List<ItemItem> requireItems = new List<ItemItem> { null, null, null, null };

    [SerializeField]private ItemItem resultItem;
    [SerializeField]private int resultQuantity = 1;

    public string RecipeName=> recipeName;
    public IReadOnlyList<ItemItem> RequireItems => requireItems;
    public ItemItem ResultItem => resultItem;
    public int ResultQuantity => resultQuantity;

#if UNITY_EDITOR
    internal void EditorSetValues(string recipeName, List<ItemItem> requireItems, ItemItem resultItem, int resultQuantity)
    {
        this.recipeName = recipeName;
        this.requireItems = requireItems != null ? new List<ItemItem>(requireItems) : new List<ItemItem>();
        this.resultItem = resultItem;
        this.resultQuantity = resultQuantity;
    }
#endif
}
