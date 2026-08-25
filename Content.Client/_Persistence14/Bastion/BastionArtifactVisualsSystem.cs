using Content.Client.Xenoarchaeology.XenoArtifacts;
using Content.Shared._Persistence14.Bastion;
using Content.Shared.Xenoarchaeology.XenoArtifacts;
using Robust.Client.GameObjects;

namespace Content.Client._Persistence14.Bastion;

/// <summary>
/// Client half of <see cref="BastionArtifactVisualsComponent"/>: toggles the artifact's UnlockingEffect
/// and ActivationEffect sprite-layer visibility from the appearance data set server-side. The layers and
/// their states are declared in the prototype's Sprite; this only flips their visibility (unlike the stock
/// RandomArtifactSprite visualizer, it never rewrites the RSI states, so the Paragon keeps its fixed art).
/// </summary>
public sealed class BastionArtifactVisualsSystem : VisualizerSystem<BastionArtifactVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BastionArtifactVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, SharedArtifactsVisuals.IsUnlocking, out var isUnlocking, args.Component))
            isUnlocking = false;

        if (!AppearanceSystem.TryGetData<bool>(uid, SharedArtifactsVisuals.IsActivated, out var isActivated, args.Component))
            isActivated = false;

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), ArtifactsVisualLayers.UnlockingEffect, out var unlockingLayer, false))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), unlockingLayer, isUnlocking);

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), ArtifactsVisualLayers.ActivationEffect, out var activationLayer, false))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), activationLayer, isActivated);
    }
}
