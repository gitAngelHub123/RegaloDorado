// Regalo Dorado — mod para PvZ Replanted (MelonLoader).
// Añade a la tienda de Crazy Dave un regalo dorado inspirado en el Brote Dorado
// de la versión móvil: cuesta $50.000, se puede comprar una vez al día y siempre
// da una planta del Jardín Zen que todavía no has conseguido.
// © 2026 Angel. Licencia MIT (ver LICENSE).

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppReloaded.Data;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(RegaloDorado.RegaloDoradoMod), "Regalo Dorado", "1.2.0", "Angel")]
[assembly: MelonGame("PopCap Games", "PvZ Replanted")]

namespace RegaloDorado;

public class RegaloDoradoMod : MelonMod
{
    // Precio en dólares tal como se ve en la tienda. El perfil guarda las monedas divididas por 10.
    internal const int PrecioVisible = 50000;
    internal const int PrecioInterno = PrecioVisible / 10;

    // Hueco libre en la tienda: página 4 (índice 3, la del Jardín Zen), segunda fila, tercera casilla.
    private const int Pagina = 3;
    private const int Casilla = 6;

    // StoreItem que el juego no usa en la tienda; nuestra lógica lo intercepta siempre.
    private const StoreItem ArticuloFalso = StoreItem.Pvz;

    internal static string Nombre => Textos.Actuales.Nombre;
    internal static string Descripcion => Textos.Actuales.Descripcion;

    // Mismas plantas que puede dar un regalo del Jardín Zen (ZenGarden.PickRandomSeedType):
    // de Peashooter (0) a Melonpult (39), sin Flowerpot (33) ni Marigold (38).
    private static readonly int[] PlantasDelJardin =
        Enumerable.Range(0, 40).Where(s => s != 33 && s != 38).ToArray();

    internal static StoreEntryData Entrada;
    private static MelonPreferences_Category s_categoria;
    private static MelonPreferences_Entry<string> s_compras;
    private static readonly System.Random s_azar = new();

    public override void OnInitializeMelon()
    {
        s_categoria = MelonPreferences.CreateCategory("RegaloDorado");
        s_compras = s_categoria.CreateEntry("UltimaCompra", "",
            description: "Día de la última compra por perfil (guid=aaaa-mm-dd;...)");
        LoggerInstance.Msg("Regalo Dorado cargado: página 4 de la tienda, $50.000, uno al día.");
    }

    internal static bool EsNuestra(StoreEntryData data) =>
        data != null && Entrada != null && data.Pointer == Entrada.Pointer;

    // Añade la entrada a la lista de la tienda que el juego cargó desde sus datos.
    internal static void AsegurarEntrada(IDataService dataService)
    {
        if (dataService == null) return;
        var lista = dataService.StoreEntryData?.TryCast<Il2CppSystem.Collections.Generic.List<StoreEntryData>>();
        if (lista == null)
        {
            Melon<RegaloDoradoMod>.Logger.Error("No se pudo acceder a la lista de artículos de la tienda.");
            return;
        }

        if (Entrada == null || Entrada.WasCollected)
        {
            StoreEntryData plantilla = null;
            for (int i = 0; i < lista.Count; i++)
                if (lista[i].StoreItem == StoreItem.PottedMarigold1) { plantilla = lista[i]; break; }
            if (plantilla == null)
            {
                Melon<RegaloDoradoMod>.Logger.Error("No encontré la caléndula para usarla de plantilla.");
                return;
            }

            // Se clona la caléndula para heredar su miniatura (el brote).
            Entrada = UnityEngine.Object.Instantiate(plantilla);
            Entrada.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Entrada.name = "Store-GoldenPresent";
            Entrada.m_entryName = Nombre;
            Entrada.m_entryDescription = Descripcion;
            Entrada.m_coinCost = new Il2CppStructArray<int>(new[] { PrecioVisible });
            Entrada.m_storeItem = ArticuloFalso;
            Entrada.m_pageIndex = Pagina;
            Entrada.m_entryIndex = Casilla;
        }

        for (int i = 0; i < lista.Count; i++)
            if (lista[i].Pointer == Entrada.Pointer) return;
        lista.Add(Entrada);
    }

    // --- Límite de una compra al día, por perfil ---

    private static Dictionary<string, string> LeerCompras()
    {
        var compras = new Dictionary<string, string>();
        foreach (var par in (s_compras.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split('=');
            if (partes.Length == 2) compras[partes[0]] = partes[1];
        }
        return compras;
    }

    private static string Hoy => DateTime.Now.ToString("yyyy-MM-dd");

    internal static bool CompradoHoy(UserProfile perfil)
    {
        if (perfil == null) return false;
        var compras = LeerCompras();
        var guid = perfil.mGuid ?? "";
        return compras.ContainsKey(guid) && compras[guid] == Hoy;
    }

    private static void MarcarCompra(UserProfile perfil)
    {
        var compras = LeerCompras();
        compras[perfil.mGuid ?? ""] = Hoy;
        s_compras.Value = string.Join(";", compras.Select(kv => kv.Key + "=" + kv.Value));
        s_categoria.SaveToFile(false);
    }

    internal static bool JardinLleno() => ZenGarden.IsZenGardenFull(true, null);

    // --- Elegir la planta ---

    private static SeedType ElegirPlanta(UserProfile perfil)
    {
        // 1) Plantas que nunca has conseguido (las que cuentan para el logro de coleccionarlas todas).
        var coleccion = perfil.mCollectedZenGardenPlants;
        var nuevas = PlantasDelJardin.Where(s => coleccion == null || s >= coleccion.Length || !coleccion[s]).ToList();
        if (nuevas.Count > 0) return (SeedType)nuevas[s_azar.Next(nuevas.Count)];

        // 2) Si ya las conseguiste todas: alguna que no esté ahora mismo en tu jardín.
        var tengo = new HashSet<int>();
        var macetas = perfil.mPottedPlant;
        for (int i = 0; macetas != null && i < perfil.mNumPottedPlants && i < macetas.Length; i++)
            if (macetas[i] != null) tengo.Add((int)macetas[i].mSeedType);
        var faltan = PlantasDelJardin.Where(s => !tengo.Contains(s)).ToList();
        if (faltan.Count > 0) return (SeedType)faltan[s_azar.Next(faltan.Count)];

        // 3) Las tienes todas: cualquiera.
        return (SeedType)PlantasDelJardin[s_azar.Next(PlantasDelJardin.Length)];
    }

    // Réplica de la compra de una caléndula (StoreModel.PurchaseSelectedItem), pero con la planta elegida.
    internal static void Comprar(StoreModel tienda)
    {
        var usuario = tienda.m_userService;
        var perfil = usuario?.ActiveUserProfile;
        if (perfil == null) return;

        if (perfil.mCoins < PrecioInterno || CompradoHoy(perfil) || JardinLleno())
        {
            tienda.RefreshPageEntries();
            return;
        }

        var planta = ElegirPlanta(perfil);
        usuario.AddCoins(-PrecioInterno);

        var zen = tienda.m_board?.mApp?.ZenGarden;
        if (zen != null)
        {
            var maceta = new PottedPlant();
            maceta.InitializePottedPlant(planta);
            zen.AddPottedPlant(maceta);
        }
        else
        {
            ZenGarden.AttemptNewPlantUserdata(planta);
        }

        MarcarCompra(perfil);
        usuario.SaveDetails();
        tienda.RefreshPageEntries();
        tienda.UpdateOtherModels();
        Melon<RegaloDoradoMod>.Logger.Msg($"Regalo Dorado comprado: {planta}");
    }
}

// --- Textos según el idioma del juego ---

internal static class Textos
{
    internal sealed class Idioma
    {
        internal readonly string Nombre, Descripcion;
        internal Idioma(string nombre, string descripcion) { Nombre = nombre; Descripcion = descripcion; }
    }

    private static readonly Idioma Espanol = new("Regalo Dorado",
        "¡Un regalo dorado! Te garantizo que trae una planta que todavía no tienes en tu jardín. ¡Solo uno al día!");
    private static readonly Idioma Ingles = new("Golden Present",
        "A golden present! I guarantee it holds a plant you don't have in your garden yet. Only one per day!");
    private static readonly Idioma Portugues = new("Presente Dourado",
        "Um presente dourado! Garanto que traz uma planta que você ainda não tem no seu jardim. Só um por dia!");
    private static readonly Idioma Frances = new("Cadeau Doré",
        "Un cadeau doré ! Je te garantis qu'il contient une plante que tu n'as pas encore dans ton jardin. Un seul par jour !");
    private static readonly Idioma Aleman = new("Goldenes Geschenk",
        "Ein goldenes Geschenk! Ich garantiere, dass darin eine Pflanze steckt, die du noch nicht in deinem Garten hast. Nur eins pro Tag!");

    // El juego traduce la clave "$LOCALE" al idioma activo: "Spanish_Spain", "English_United States", etc.
    // Se consulta cada vez porque el jugador puede cambiar el idioma en las opciones.
    internal static Idioma Actuales
    {
        get
        {
            string locale = "";
            try { locale = Il2CppTekly.Localizations.Localizer.Instance?.Localize("$LOCALE") ?? ""; }
            catch (Exception) { }

            if (locale.StartsWith("Spanish")) return Espanol;
            if (locale.StartsWith("Portuguese")) return Portugues;
            if (locale.StartsWith("French")) return Frances;
            if (locale.StartsWith("German")) return Aleman;
            return Ingles;
        }
    }
}

// --- Miniatura dorada ---

internal static class Miniatura
{
    private static readonly HashSet<IntPtr> s_modelosNuestros = new();
    private static readonly HashSet<IntPtr> s_spritesDorados = new();
    private static readonly Dictionary<IntPtr, Sprite> s_cache = new();

    internal static void Marcar(AddressableSpriteValueModel modelo, bool esNuestra)
    {
        if (modelo == null) return;
        if (esNuestra) s_modelosNuestros.Add(modelo.Pointer);
        else s_modelosNuestros.Remove(modelo.Pointer);
    }

    internal static void DorarSiEsNuestra(AddressableSpriteValueModel modelo)
    {
        if (modelo == null || !s_modelosNuestros.Contains(modelo.Pointer)) return;
        var original = modelo.Value;
        if (original == null || s_spritesDorados.Contains(original.Pointer)) return;
        try
        {
            // Se usa la caja de regalo que sueltan los zombis; si no se puede cargar, se dora el brote.
            var dorado = Dorar(Regalo() ?? original);
            if (dorado != null) modelo.Value = dorado;
        }
        catch (Exception e) { Melon<RegaloDoradoMod>.Logger.Error("No se pudo dorar la miniatura: " + e); }
    }

    private static readonly string[] s_clavesRegalo =
        { "Assets/Art/Coins/Sprites/SPR_Present.png", "SPR_Present.png", "SPR_Present" };
    private static Sprite s_regalo;
    private static bool s_regaloBuscado;

    private static Sprite Regalo()
    {
        if (s_regalo != null && !s_regalo.WasCollected) return s_regalo;
        if (s_regaloBuscado) return null;
        s_regaloBuscado = true;

        foreach (var clave in s_clavesRegalo)
        {
            try
            {
                var clave2 = new Il2CppSystem.Object(Il2CppInterop.Runtime.IL2CPP.ManagedStringToIl2Cpp(clave));
                var sprite = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(clave2).WaitForCompletion();
                if (sprite != null) { s_regalo = sprite; break; }
            }
            catch (Exception) { }
        }

        if (s_regalo == null)
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite.name == "SPR_Present") { s_regalo = sprite; break; }

        if (s_regalo == null)
            Melon<RegaloDoradoMod>.Logger.Warning("No encontré la imagen del regalo; se usará el brote dorado.");
        else
            s_regalo.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return s_regalo;
    }

    private static Sprite Dorar(Sprite original)
    {
        if (s_cache.TryGetValue(original.Pointer, out var guardado) && guardado != null && !guardado.WasCollected)
            return guardado;

        // Las texturas del juego no son legibles desde código: se copian a la GPU y se leen de vuelta.
        var tex = original.texture;
        var area = original.textureRect;
        int ancho = Mathf.RoundToInt(area.width), alto = Mathf.RoundToInt(area.height);

        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        var anterior = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var copia = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(area.x, area.y, ancho, alto), 0, 0);
        copia.Apply();
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(rt);

        // Pasa cada píxel a una escala de oro según su brillo, con brillos casi blancos en las zonas claras.
        var pixeles = copia.GetPixels32();
        for (int i = 0; i < pixeles.Length; i++)
        {
            var p = pixeles[i];
            if (p.a == 0) continue;
            float luz = (0.30f * p.r + 0.59f * p.g + 0.11f * p.b) / 255f;
            luz = Mathf.Clamp01(luz * 1.35f);
            Color oro = luz < 0.5f
                ? Color.Lerp(new Color(0.35f, 0.20f, 0.02f), new Color(0.95f, 0.70f, 0.10f), luz * 2f)
                : Color.Lerp(new Color(0.95f, 0.70f, 0.10f), new Color(1.00f, 0.96f, 0.65f), (luz - 0.5f) * 2f);
            pixeles[i] = new Color32((byte)(oro.r * 255), (byte)(oro.g * 255), (byte)(oro.b * 255), p.a);
        }
        copia.SetPixels32(pixeles);
        copia.Apply();
        copia.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var pivote = new Vector2(original.pivot.x / area.width, original.pivot.y / area.height);
        var dorado = Sprite.Create(copia, new Rect(0, 0, ancho, alto), pivote, original.pixelsPerUnit);
        dorado.name = "RegaloDorado";
        dorado.hideFlags = HideFlags.DontUnloadUnusedAsset;

        s_spritesDorados.Add(dorado.Pointer);
        s_cache[original.Pointer] = dorado;
        return dorado;
    }
}

// --- Parches ---

[HarmonyPatch(typeof(StorePageModel), nameof(StorePageModel.UpdateEntries))]
internal static class Parche_UpdateEntries
{
    private static void Prefix(StorePageModel __instance) =>
        RegaloDoradoMod.AsegurarEntrada(__instance.m_dataService);
}

[HarmonyPatch(typeof(StoreEntryModel), nameof(StoreEntryModel.IsItemSoldOut))]
internal static class Parche_IsItemSoldOut
{
    private static bool Prefix(StoreEntryModel __instance, ref bool __result)
    {
        if (!RegaloDoradoMod.EsNuestra(__instance.m_entryData)) return true;
        __result = RegaloDoradoMod.CompradoHoy(__instance.m_userService?.ActiveUserProfile)
                   || RegaloDoradoMod.JardinLleno();
        return false;
    }
}

[HarmonyPatch(typeof(StoreEntryModel), nameof(StoreEntryModel.IsComingSoon))]
internal static class Parche_IsComingSoon
{
    private static bool Prefix(StoreEntryModel __instance, ref bool __result)
    {
        if (!RegaloDoradoMod.EsNuestra(__instance.m_entryData)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(StoreEntryModel), nameof(StoreEntryModel.IsItemUnavailable))]
internal static class Parche_IsItemUnavailable
{
    private static bool Prefix(StoreEntryModel __instance, ref bool __result)
    {
        if (!RegaloDoradoMod.EsNuestra(__instance.m_entryData)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(StoreEntryModel), nameof(StoreEntryModel.UpdateModelData))]
internal static class Parche_UpdateModelData
{
    private static void Postfix(StoreEntryModel __instance)
    {
        var esNuestra = RegaloDoradoMod.EsNuestra(__instance.m_entryData);
        // El mismo modelo de miniatura puede mostrar otro artículo después (p. ej. la ventana de confirmar compra).
        Miniatura.Marcar(__instance.m_thumbnailModel, esNuestra);
        if (!esNuestra) return;
        Miniatura.DorarSiEsNuestra(__instance.m_thumbnailModel);
        // El juego trata el nombre como clave de traducción; aquí ponemos el texto tal cual.
        if (__instance.m_nameModel != null)
        {
            __instance.m_nameModel.m_needsLocalization = false;
            __instance.m_nameModel.Value = RegaloDoradoMod.Nombre;
        }
        if (__instance.m_descriptionModel != null)
        {
            __instance.m_descriptionModel.m_needsLocalization = false;
            __instance.m_descriptionModel.Value = RegaloDoradoMod.Descripcion;
        }
    }
}

// La miniatura se carga de forma asíncrona; al terminar, si el modelo es el del Regalo Dorado, se dora.
[HarmonyPatch(typeof(AddressableSpriteValueModel), nameof(AddressableSpriteValueModel.OnSpriteLoaded))]
internal static class Parche_OnSpriteLoaded
{
    private static void Postfix(AddressableSpriteValueModel __instance) => Miniatura.DorarSiEsNuestra(__instance);
}

[HarmonyPatch(typeof(StoreEntryModel), nameof(StoreEntryModel.SetBubbleText))]
internal static class Parche_SetBubbleText
{
    private static void Postfix(StoreEntryModel __instance)
    {
        if (!RegaloDoradoMod.EsNuestra(__instance.m_entryData)) return;
        var dave = __instance.m_storeModel?.m_daveSaying;
        if (dave == null) return;
        dave.m_needsLocalization = false;
        dave.Value = RegaloDoradoMod.Descripcion;
    }
}

[HarmonyPatch(typeof(StoreModel), nameof(StoreModel.PurchaseSelectedItem))]
internal static class Parche_PurchaseSelectedItem
{
    private static bool Prefix(StoreModel __instance)
    {
        if (!RegaloDoradoMod.EsNuestra(__instance.m_purchaseData)) return true;
        try { RegaloDoradoMod.Comprar(__instance); }
        catch (Exception e) { Melon<RegaloDoradoMod>.Logger.Error(e); }
        return false;
    }
}
