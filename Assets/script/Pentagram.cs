using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Pentagram : MonoBehaviour
{
    [Header("Настройки")]
    public ItemData stoneItem;
    public GameObject[] stonesOnAltar;
    public int totalCandles = 5;

    [Header("Эффекты изгнания")]
    public AudioClip exorcismSound;
    public Light flashLight;
    public Color burnColor = new Color(1f, 0.4f, 0f);

    [Header("Скрытие UI и рук")]
    public GameObject hudToHide;
    public GameObject handsToHide;

    [Header("Катсцена")]
    public GameObject cutsceneCamera;
    public float timeBeforeBurn = 2f;

    [Header("Бабки")]
    public GameObject ritualGhost;

    private int stonesPlaced = 0;
    private int candlesLit = 0;
    private bool ritualDone = false;

    void Start()
    {
        if (cutsceneCamera != null) cutsceneCamera.SetActive(false);
        if (ritualGhost != null) ritualGhost.SetActive(false);
    }

    public bool PlaceStone(Inventory playerInventory)
    {
        if (ritualDone || stonesPlaced >= stonesOnAltar.Length) return false;

        if (playerInventory.Has(stoneItem))
        {
            playerInventory.Remove(stoneItem, 1);

            if (stonesOnAltar[stonesPlaced] != null)
                stonesOnAltar[stonesPlaced].SetActive(true);

            stonesPlaced++;
            CheckRitualConditions();

            return true;
        }
        return false;
    }

    public void OnCandleLit()
    {
        candlesLit++;
        CheckRitualConditions();
    }

    void CheckRitualConditions()
    {
        Debug.Log($"Камни: {stonesPlaced}/{stonesOnAltar.Length} | Свечи: {candlesLit}/{totalCandles}");

        // Если разложены все камни И зажжены все свечи
        if (stonesPlaced >= stonesOnAltar.Length && candlesLit >= totalCandles && !ritualDone)
        {
            StartCoroutine(ActivateRitual());
        }
    }

    IEnumerator ActivateRitual()
    {
        ritualDone = true;

        if (QuestManager.Instance != null) QuestManager.Instance.SetQuestStep(4);

        GhostChase realGhost = FindObjectOfType<GhostChase>();
        PlayerController pc = FindObjectOfType<PlayerController>();
        Camera mainCam = Camera.main;

        if (pc == null) yield break;

        if (handsToHide != null) handsToHide.SetActive(false);
        bool hudWasActive = false;
        if (hudToHide != null)
        {
            hudWasActive = hudToHide.activeSelf;
            hudToHide.SetActive(false);
        }

        pc.enabled = false;

        bool hasCutsceneCam = cutsceneCamera != null;
        if (hasCutsceneCam)
        {
            if (mainCam != null) mainCam.gameObject.SetActive(false);
            cutsceneCamera.SetActive(true);
        }

        if (realGhost != null) realGhost.gameObject.SetActive(false);
        if (ritualGhost != null) ritualGhost.SetActive(true);

        if (exorcismSound != null)
        {
            AudioSource playerAudio = pc.GetComponent<AudioSource>();
            if (playerAudio == null) playerAudio = pc.gameObject.AddComponent<AudioSource>();
            playerAudio.clip = exorcismSound;
            playerAudio.spatialBlend = 0f;
            playerAudio.Play();
        }

        if (flashLight != null && ritualGhost != null)
        {
            flashLight.transform.position = ritualGhost.transform.position + Vector3.up * 1.5f;
            flashLight.enabled = true;
        }

        List<Material> ghostMaterials = new List<Material>();
        if (ritualGhost != null)
        {
            Renderer[] ghostRenderers = ritualGhost.GetComponentsInChildren<Renderer>();
            foreach (var r in ghostRenderers)
            {
                foreach (var mat in r.materials)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                    ghostMaterials.Add(mat);
                }
            }
        }

        yield return new WaitForSeconds(timeBeforeBurn);

        float duration = 5.0f;
        float elapsed = 0f;

        if (ritualGhost != null)
        {
            Vector3 startScale = ritualGhost.transform.localScale;
            Vector3 startPos = ritualGhost.transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                Color currentEmission = Color.Lerp(Color.black, burnColor * 8f, Mathf.Pow(t, 2f));
                foreach (var mat in ghostMaterials)
                    mat.SetColor("_EmissionColor", currentEmission);

                float shake = Mathf.Sin(elapsed * 50f) * 0.05f;
                ritualGhost.transform.position = startPos + new Vector3(shake, 0, shake);

                if (t > 0.5f)
                {
                    ritualGhost.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, (t - 0.5f) * 2f);
                }

                yield return null;
            }

            ritualGhost.SetActive(false);
        }

        if (flashLight != null) flashLight.enabled = false;

        yield return new WaitForSeconds(1.5f);

        if (hasCutsceneCam)
        {
            cutsceneCamera.SetActive(false);
            if (mainCam != null) mainCam.gameObject.SetActive(true);
        }

        if (handsToHide != null) handsToHide.SetActive(true);
        if (hudToHide != null && hudWasActive) hudToHide.SetActive(true);

        if (pc != null) pc.enabled = true;
    }
}