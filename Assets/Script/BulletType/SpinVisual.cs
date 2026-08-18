using UnityEngine;

/// <summary>
/// Чисто косметическое вращение модели снаряда в полёте. Вешается на дочерний
/// визуал, а не на корень с Bullet — Bullet сам каждый кадр выставляет
/// transform.rotation корня по направлению движения, и если крутить тот же
/// transform, вращение будет тут же перезатираться.
/// </summary>
public class SpinVisual : MonoBehaviour
{
    [Tooltip("Скорость вращения по каждой оси, градусов/сек.")]
    public Vector3 degreesPerSecond = new Vector3(90f, 140f, 60f);

    private void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
