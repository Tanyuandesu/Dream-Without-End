using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SYS7 read-only pause-menu view for collected run items.
///
/// The viewer never owns item progression. It reads a snapshot from the
/// authoritative ItemManager and only creates buttons for definitions that
/// are already collected. Item UI text is resolved through stable ItemId-
/// based localization keys, so future text replacement does not touch code.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemViewerController : MonoBehaviour
{
    private const string NameSuffix = "NAME";
    private const string DescriptionSuffix = "DESC";

    private readonly List<Button> itemButtons = new List<Button>();

    private ItemManager itemManager;
    private RectTransform listRoot;
    private TextMeshProUGUI emptyListLabel;
    private GameObject detailsRoot;
    private Image iconImage;
    private TextMeshProUGUI itemNameText;
    private LocalizedTMPText localizedItemName;
    private TextMeshProUGUI itemDescriptionText;
    private LocalizedTMPText localizedItemDescription;
    private TextMeshProUGUI noSelectionText;

    private ItemDefinition selectedItem;
    private bool controlsBuilt;

    public Button FirstSelectableButton =>
        itemButtons.Count > 0 ? itemButtons[0] : null;

    public void BuildRuntimeControls()
    {
        if (controlsBuilt)
        {
            return;
        }

        controlsBuilt = true;

        BuildListColumn();
        BuildDetailsColumn();
        RefreshFromProgress();
    }

    private void OnEnable()
    {
        ResolveItemManager();
        SubscribeToItemProgress();

        if (controlsBuilt)
        {
            RefreshFromProgress();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromItemProgress();
    }

    private void OnDestroy()
    {
        UnsubscribeFromItemProgress();
    }

    public void RefreshFromProgress()
    {
        if (!controlsBuilt)
        {
            return;
        }

        ResolveItemManager();
        ClearItemButtons();

        ItemProgressSnapshot snapshot = itemManager != null
            ? itemManager.CreateProgressSnapshot()
            : null;

        IReadOnlyList<ItemDefinition> collected = snapshot != null
            ? snapshot.CollectedItems
            : null;

        if (collected == null || collected.Count == 0)
        {
            if (emptyListLabel != null)
            {
                emptyListLabel.gameObject.SetActive(true);
            }

            SelectItem(null);
            return;
        }

        if (emptyListLabel != null)
        {
            emptyListLabel.gameObject.SetActive(false);
        }

        for (int i = 0; i < collected.Count; i++)
        {
            ItemDefinition definition = collected[i];
            if (definition == null)
            {
                continue;
            }

            Button button = CreateItemButton(
                listRoot,
                definition,
                i);

            itemButtons.Add(button);
        }

        ItemDefinition nextSelection = null;

        if (selectedItem != null)
        {
            for (int i = 0; i < collected.Count; i++)
            {
                ItemDefinition definition = collected[i];
                if (definition != null &&
                    definition.ItemId == selectedItem.ItemId)
                {
                    nextSelection = definition;
                    break;
                }
            }
        }

        if (nextSelection == null)
        {
            for (int i = 0; i < collected.Count; i++)
            {
                if (collected[i] != null)
                {
                    nextSelection = collected[i];
                    break;
                }
            }
        }

        SelectItem(nextSelection);
    }

    public void SelectFirstButtonForNavigation()
    {
        Button first = FirstSelectableButton;
        if (first != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    private void ResolveItemManager()
    {
        if (itemManager != null)
        {
            return;
        }

        itemManager = FindFirstObjectByType<ItemManager>();
    }

    private void SubscribeToItemProgress()
    {
        if (itemManager == null)
        {
            return;
        }

        itemManager.ProgressChanged -= HandleItemProgressChanged;
        itemManager.ProgressChanged += HandleItemProgressChanged;
    }

    private void UnsubscribeFromItemProgress()
    {
        if (itemManager != null)
        {
            itemManager.ProgressChanged -= HandleItemProgressChanged;
        }
    }

    private void HandleItemProgressChanged(ItemProgressSnapshot snapshot)
    {
        RefreshFromProgress();
    }

    private void SelectItem(ItemDefinition definition)
    {
        selectedItem = definition;

        bool hasSelection = definition != null;

        if (detailsRoot != null)
        {
            detailsRoot.SetActive(hasSelection);
        }

        if (noSelectionText != null)
        {
            noSelectionText.gameObject.SetActive(!hasSelection);
        }

        if (!hasSelection)
        {
            return;
        }

        string nameKey = BuildItemLocalizationKey(
            definition.ItemId,
            NameSuffix);

        string descriptionKey = BuildItemLocalizationKey(
            definition.ItemId,
            DescriptionSuffix);

        if (localizedItemName != null)
        {
            localizedItemName.SetKey(nameKey);
        }

        if (localizedItemDescription != null)
        {
            localizedItemDescription.SetKey(descriptionKey);
        }

        if (iconImage != null)
        {
            iconImage.sprite = definition.Icon;
            iconImage.enabled = definition.Icon != null;
        }
    }

    private void BuildListColumn()
    {
        GameObject listBackground = CreateRect("CollectedList", transform);
        RectTransform backgroundRect = listBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(-330f, -15f);
        backgroundRect.sizeDelta = new Vector2(360f, 430f);

        Image background = listBackground.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.11f, 0.78f);

        listRoot = listBackground.GetComponent<RectTransform>();

        GameObject emptyObject = CreateRect("EmptyListLabel", listBackground.transform);
        RectTransform emptyRect = emptyObject.GetComponent<RectTransform>();
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(26f, 26f);
        emptyRect.offsetMax = new Vector2(-26f, -26f);

        emptyListLabel = emptyObject.AddComponent<TextMeshProUGUI>();
        emptyListLabel.font = TMP_Settings.defaultFontAsset;
        emptyListLabel.fontSize = 24f;
        emptyListLabel.alignment = TextAlignmentOptions.Center;
        emptyListLabel.color = new Color(0.78f, 0.80f, 0.83f, 1f);
        emptyListLabel.enableWordWrapping = true;
        emptyListLabel.raycastTarget = false;

        LocalizedTMPText localizedEmpty = emptyObject.AddComponent<LocalizedTMPText>();
        localizedEmpty.SetKey("UI_ITEMS_EMPTY");
    }

    private void BuildDetailsColumn()
    {
        GameObject detailsBackground = CreateRect("ItemDetails", transform);
        RectTransform backgroundRect = detailsBackground.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(215f, -15f);
        backgroundRect.sizeDelta = new Vector2(650f, 430f);

        Image background = detailsBackground.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.11f, 0.78f);

        detailsRoot = CreateRect("SelectedItemContent", detailsBackground.transform);
        StretchFull(detailsRoot.GetComponent<RectTransform>());

        GameObject iconObject = CreateRect("Icon", detailsRoot.transform);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(34f, -34f);
        iconRect.sizeDelta = new Vector2(120f, 120f);

        iconImage = iconObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false;

        GameObject nameObject = CreateRect("ItemName", detailsRoot.transform);
        RectTransform nameRect = nameObject.GetComponent<RectTransform>();
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(180f, -44f);
        nameRect.sizeDelta = new Vector2(430f, 80f);

        itemNameText = nameObject.AddComponent<TextMeshProUGUI>();
        itemNameText.font = TMP_Settings.defaultFontAsset;
        itemNameText.fontSize = 32f;
        itemNameText.fontStyle = FontStyles.Bold;
        itemNameText.alignment = TextAlignmentOptions.MidlineLeft;
        itemNameText.color = Color.white;
        itemNameText.enableWordWrapping = true;
        itemNameText.raycastTarget = false;

        localizedItemName = nameObject.AddComponent<LocalizedTMPText>();

        GameObject descriptionObject = CreateRect("ItemDescription", detailsRoot.transform);
        RectTransform descriptionRect = descriptionObject.GetComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.offsetMin = new Vector2(34f, 34f);
        descriptionRect.offsetMax = new Vector2(-34f, -175f);

        itemDescriptionText = descriptionObject.AddComponent<TextMeshProUGUI>();
        itemDescriptionText.font = TMP_Settings.defaultFontAsset;
        itemDescriptionText.fontSize = 25f;
        itemDescriptionText.alignment = TextAlignmentOptions.TopLeft;
        itemDescriptionText.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        itemDescriptionText.enableWordWrapping = true;
        itemDescriptionText.overflowMode = TextOverflowModes.Overflow;
        itemDescriptionText.raycastTarget = false;

        localizedItemDescription = descriptionObject.AddComponent<LocalizedTMPText>();

        GameObject noSelectionObject = CreateRect("NoSelectionLabel", detailsBackground.transform);
        StretchFull(noSelectionObject.GetComponent<RectTransform>());

        noSelectionText = noSelectionObject.AddComponent<TextMeshProUGUI>();
        noSelectionText.font = TMP_Settings.defaultFontAsset;
        noSelectionText.fontSize = 24f;
        noSelectionText.alignment = TextAlignmentOptions.Center;
        noSelectionText.color = new Color(0.78f, 0.80f, 0.83f, 1f);
        noSelectionText.enableWordWrapping = true;
        noSelectionText.raycastTarget = false;

        LocalizedTMPText localizedNoSelection = noSelectionObject.AddComponent<LocalizedTMPText>();
        localizedNoSelection.SetKey("UI_ITEMS_NO_SELECTION");
    }

    private Button CreateItemButton(
        Transform parent,
        ItemDefinition definition,
        int index)
    {
        GameObject buttonObject = CreateRect(
            "ItemButton_" + definition.ItemId,
            parent);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f - (index * 56f));
        rect.sizeDelta = new Vector2(312f, 48f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ItemDefinition captured = definition;
        button.onClick.AddListener(() => SelectItem(captured));

        GameObject labelObject = CreateRect("Label", buttonObject.transform);
        StretchFull(labelObject.GetComponent<RectTransform>());

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 21f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        LocalizedTMPText localized = labelObject.AddComponent<LocalizedTMPText>();
        localized.SetKey(BuildItemLocalizationKey(definition.ItemId, NameSuffix));

        return button;
    }

    private void ClearItemButtons()
    {
        for (int i = 0; i < itemButtons.Count; i++)
        {
            Button button = itemButtons[i];
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        itemButtons.Clear();
    }

    private static string BuildItemLocalizationKey(string itemId, string suffix)
    {
        string normalized = NormalizeIdForLocalizationKey(itemId);
        return "ITEM_" + normalized + "_" + suffix;
    }

    private static string NormalizeIdForLocalizationKey(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return "MISSING_ID";
        }

        StringBuilder builder = new StringBuilder(itemId.Length);
        bool previousUnderscore = false;

        string trimmed = itemId.Trim();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char character = trimmed[i];

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousUnderscore = false;
                continue;
            }

            if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    [ContextMenu("SYS7 Debug/Print Collected Item Snapshot")]
    private void DebugPrintCollectedItems()
    {
        ResolveItemManager();

        if (itemManager == null)
        {
            Debug.Log("[SYS7] ItemManager not found.", this);
            return;
        }

        ItemProgressSnapshot snapshot = itemManager.CreateProgressSnapshot();
        IReadOnlyList<ItemDefinition> collected = snapshot.CollectedItems;

        if (collected.Count == 0)
        {
            Debug.Log("[SYS7] Collected items: 0", this);
            return;
        }

        List<string> ids = new List<string>();

        for (int i = 0; i < collected.Count; i++)
        {
            if (collected[i] != null)
            {
                ids.Add(collected[i].ItemId);
            }
        }

        Debug.Log(
            "[SYS7] Collected items: " + ids.Count +
            " | " + string.Join(", ", ids),
            this);
    }
#endif
}
