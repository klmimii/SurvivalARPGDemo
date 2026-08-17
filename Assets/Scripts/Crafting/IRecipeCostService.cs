public interface IRecipeCostService
{
    bool IsUnlocked(RecipeDefinition recipe);

    bool CanConsumeIngredients(
        RecipeDefinition recipe,
        int craftCount = 1);

    InventoryOperationResult TryConsumeIngredients(
        RecipeDefinition recipe,
        int craftCount = 1);

    InventoryOperationResult TryRefundIngredients(
        RecipeDefinition recipe,
        int craftCount = 1,
        float refundRate = 1f);
}