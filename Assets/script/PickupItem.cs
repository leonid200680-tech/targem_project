using System.Collections.Generic;
using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    public string prompt = "Нажмите E, чтобы подобрать";

    public static int stonesCollected = 0;

    // Подобранные предметы не уничтожаются, а прячутся: их можно выбросить обратно
    private static readonly Dictionary<ItemData, Stack<PickupItem>> stored = new Dictionary<ItemData, Stack<PickupItem>>();
    private bool counted;

    public void Pickup(Inventory playerInventory)
    {
        if (playerInventory == null) return;

        if (playerInventory.Add(item, amount))
        {
            if (item.type == ItemType.Stone && !counted)
            {
                counted = true;
                stonesCollected++;
                HallucinationManager hm = FindObjectOfType<HallucinationManager>();

                if (hm != null)
                {
                    if (stonesCollected == 1)
                    {
                        hm.PlayCreepyNoise();
                    }
                    else if (stonesCollected == 2)
                    {
                        hm.TriggerSecondStoneEvent(); // Вызываем метод с картинкой и бабкой
                    }
                    else
                    {
                        hm.PlayCreepyNoise();
                        var scares = FindAnyObjectByType<ScareDirector>();
                        if (scares != null) scares.StoneScare();
                    }
                }
            }
            if (!stored.TryGetValue(item, out var stack)) stored[item] = stack = new Stack<PickupItem>();
            stack.Push(this);
            gameObject.SetActive(false);
        }
    }

    // Вернуть в мир один подобранный предмет. false - такого не поднимали (выбросить нельзя)
    // from - откуда предмет летит (из руки); без него сразу лежит на месте
    public static bool Drop(ItemData item, Vector3 ground, Vector3? from = null)
    {
        if (item == null || !stored.TryGetValue(item, out var stack)) return false;
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (p == null) continue;   // остался от прошлой загрузки сцены
            p.transform.SetParent(null, true);
            p.transform.position = ground;
            p.gameObject.SetActive(true);
            // Низ всей модели (у неё может быть несколько частей) ставим точно на пол
            float bottom = float.MaxValue;
            foreach (var r in p.GetComponentsInChildren<Renderer>()) bottom = Mathf.Min(bottom, r.bounds.min.y);
            Vector3 landed = bottom < float.MaxValue ? ground + Vector3.up * (ground.y - bottom + 0.002f) : ground;
            p.transform.position = landed;
            if (from != null) p.StartCoroutine(p.Fall(from.Value, landed));
            return true;
        }
        return false;
    }

    // Падение по дуге с вращением. Пока летит, предмет нельзя подобрать и он ни во что не врезается
    System.Collections.IEnumerator Fall(Vector3 from, Vector3 landed)
    {
        const float time = 0.4f;
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;
        Quaternion endRot = transform.rotation;
        Vector3 axis = Random.onUnitSphere;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            transform.position = Vector3.Lerp(from, landed, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
            transform.rotation = Quaternion.AngleAxis((1f - k) * 360f, axis) * endRot;
            yield return null;
        }
        transform.SetPositionAndRotation(landed, endRot);
        foreach (var c in colliders) c.enabled = true;
    }
}
