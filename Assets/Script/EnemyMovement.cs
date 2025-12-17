using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 3f;

    private Transform tower;

    private void Start()
    {
        // Ищем объект с тегом Tower
        GameObject towerObj = GameObject.FindGameObjectWithTag("Player");
        if (towerObj != null)
        {
            tower = towerObj.transform;
        }
        else
        {
            Debug.LogError("Не найден объект с тегом Player!");
        }
    }

    private void Update()
    {
        if (tower == null) return;

        // Берём позицию башни по XZ, оставляем свою высоту по Y
        Vector3 targetPos = new Vector3(tower.position.x, transform.position.y, tower.position.z);
        Vector3 dir = (targetPos - transform.position).normalized;

        // Движение по прямой к башне
        transform.position += dir * speed * Time.deltaTime;

        // Поворачиваем врага лицом к башне (не обязательно, но приятно)
        transform.LookAt(targetPos);
    }
}