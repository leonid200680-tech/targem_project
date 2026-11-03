using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Скример "бабка в лицо" вместо картинки на весь экран: копия настоящей бабки появляется
// прямо перед камерой, с криком бросается к лицу, свет мигает, камеру трясёт. Потом резкая темнота.
public class GrannyJumpscare : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    // source - настоящая бабка (берётся только её модель и анимация)
    public static void Play(GameObject source, AudioClip scream, float duration = 1.3f)
    {
        var cam = Camera.main;
        if (source == null || cam == null || IsPlaying) return;
        var go = new GameObject("Granny Jumpscare");
        go.AddComponent<GrannyJumpscare>().StartCoroutine(go.GetComponent<GrannyJumpscare>().Run(source, scream, cam, duration));
    }

    IEnumerator Run(GameObject source, AudioClip scream, Camera cam, float duration)
    {
        IsPlaying = true;
        var pc = FindAnyObjectByType<PlayerController>();
        bool hadControl = pc != null && pc.enabled;
        if (pc != null) pc.enabled = false;

        var granny = MakeCopy(source);
        var anim = granny.GetComponent<Animator>();
        Transform head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        granny.SetActive(true);
        if (anim != null) { anim.Play("скример", 0, 0.15f); anim.Update(0f); }
        float headHeight = head != null ? head.position.y - granny.transform.position.y : 1.55f;

        // Свет снизу-сбоку на лицо: сквозь темноту видно морщины и глаза
        var lightGo = new GameObject("Jumpscare Light");
        lightGo.transform.SetParent(cam.transform, false);
        lightGo.transform.localPosition = new Vector3(0.25f, -0.35f, 0.05f);
        var face = lightGo.AddComponent<Light>();
        var hd = lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        face.type = LightType.Point;
        face.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        face.range = 3f;
        face.color = new Color(1f, 0.72f, 0.55f);
        face.shadows = LightShadows.None;
        hd.volumetricDimmer = 0f;

        // На время скримера без размытия движения: тряска иначе смазывает лицо в кашу
        var volGo = new GameObject("Jumpscare Volume");
        var vol = volGo.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var mb = profile.Add<UnityEngine.Rendering.HighDefinition.MotionBlur>(true);
        mb.intensity.Override(0f);
        vol.sharedProfile = profile;

        // Вспышка/затемнение поверх всего
        var canvasGo = new GameObject("Jumpscare Flash");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var flash = new GameObject("Flash").AddComponent<Image>();
        flash.transform.SetParent(canvasGo.transform, false);
        flash.rectTransform.anchorMin = Vector2.zero;
        flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.sizeDelta = Vector2.zero;
        flash.raycastTarget = false;
        flash.color = new Color(1f, 0.95f, 0.9f, 0.55f);

        if (scream != null)
        {
            var src = cam.gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.PlayOneShot(scream, 1f);
            Destroy(src, scream.length + 0.2f);
        }

        Transform camT = cam.transform;
        Vector3 camLocal = camT.localPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;

            // Бросок: с 1.1 м до 0.32 м за первые 0.25 с, дальше дёргается у самого лица
            float dist = Mathf.Lerp(1.1f, 0.32f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f)));
            dist += Mathf.Sin(t * 37f) * 0.012f;
            Vector3 fwd = camT.forward;
            Vector3 headPos = camT.position + fwd * dist + camT.up * -0.1f;
            Vector3 flat = new Vector3(-fwd.x, 0f, -fwd.z);
            if (flat.sqrMagnitude < 1e-4f) flat = -camT.up;
            granny.transform.SetPositionAndRotation(headPos - Vector3.up * headHeight, Quaternion.LookRotation(flat.normalized));
            if (head != null)
            {
                // голову подтягиваем точно к камере (модель может быть ниже/выше)
                Vector3 off = headPos - head.position;
                granny.transform.position += off;
            }

            // Свет рвано мигает, как от искрящей лампы
            face.intensity = (Random.value < 0.8f ? 1f : 0.2f) * Mathf.Lerp(110f, 70f, k);
            flash.color = new Color(1f, 0.95f, 0.9f, Mathf.Max(0f, 0.55f - t * 4f));

            // Тряска камеры
            float shake = Mathf.Lerp(0.012f, 0.004f, k);
            camT.localPosition = camLocal + Random.insideUnitSphere * shake;
            yield return null;
        }

        // Резко - в темноту
        camT.localPosition = camLocal;
        Destroy(granny);
        Destroy(lightGo);
        Destroy(volGo);
        Destroy(profile);
        flash.color = Color.black;
        yield return new WaitForSeconds(0.35f);
        for (float f = 0f; f < 0.4f; f += Time.deltaTime)
        {
            flash.color = new Color(0f, 0f, 0f, 1f - f / 0.4f);
            yield return null;
        }
        Destroy(canvasGo);

        if (pc != null && hadControl) pc.enabled = true;
        IsPlaying = false;
        Destroy(gameObject);
    }

    // Копия бабки без поведения: только модель и анимация
    static GameObject MakeCopy(GameObject src)
    {
        var holder = new GameObject("tmp");
        holder.SetActive(false);   // копия создаётся выключенной, её скрипты не успеют запуститься
        var go = Instantiate(src, holder.transform);
        go.name = "Jumpscare Granny";
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(b);
        foreach (var a in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(a);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var s in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(s);
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        go.transform.SetParent(null, false);
        go.SetActive(false);
        Destroy(holder);
        return go;
    }
}
