using UnityEngine;
public class PlayerInteraction
{
    private readonly GameObject interactUI;
    private readonly InventoryManager inventory;

    private Interact currentInteract;
    private ItemClass currentItem;
    private ShopNPC currentShopNPC;

    public PlayerInteraction(GameObject interactUI, InventoryManager inventory)
    {
        this.interactUI = interactUI;
        this.inventory = inventory;
    }

    public void HandleTriggerEnter(Collider2D collision)
    {
        Interact interact = collision.GetComponent<Interact>();
        if (interact != null)
        {
            currentInteract = interact;
            ShowInteractUI(collision.transform.position);
        }

        ItemClass item = collision.GetComponent<ItemClass>();
        if (item != null)
        {
            currentItem = item;
            ShowInteractUI(currentItem.transform.position);
        }

        ShopNPC shop = collision.GetComponent<ShopNPC>();
        if (shop != null)
        {
            currentShopNPC = shop;
            ShowInteractUI(collision.transform.position);
        }
    }

    public void HandleTriggerExit(Collider2D collision)
    {
        Interact interact = collision.GetComponent<Interact>();
        if (interact != null && interact == currentInteract)
        {
            currentInteract = null;
            HideInteractUI();
        }

        ItemClass item = collision.GetComponent<ItemClass>();
        if (item != null && item == currentItem)
        {
            currentItem = null;
            HideInteractUI();
        }

        ShopNPC shop = collision.GetComponent<ShopNPC>();
        if (shop != null && shop == currentShopNPC)
        {
            currentShopNPC = null;
            HideInteractUI();

            // Tự động đóng Shop nếu người chơi đi ra xa
            if (ShopController.Instance != null) ShopController.Instance.CloseShop();
        }
    }

    public void TryInteract()
    {
        if (currentInteract != null)
        {
            currentInteract.InteractWithObject();
        }
        if (currentItem != null && currentItem.isActiveAndEnabled)
        {
            inventory.AddItem(currentItem.itemData, currentItem.amount);
            PoolManager.Despawn(currentItem.gameObject);

            currentItem = null;
            HideInteractUI();
        }
        if (currentShopNPC != null)
        {
            currentShopNPC.Interact();
        }
    }

    private void ShowInteractUI(Vector3 worldPosition)
    {
        if (interactUI == null) return;
        interactUI.SetActive(true);
        interactUI.transform.position = worldPosition + new Vector3(0, 0.5f, 0);
    }

    private void HideInteractUI()
    {
        if (interactUI != null) interactUI.SetActive(false);
    }

    public void ClearAll()
    {
        currentInteract = null;
        currentItem = null;
        currentShopNPC = null;
        HideInteractUI();
    }
}
