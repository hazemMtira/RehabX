using UnityEngine;
using UnityEngine.AI;

public class AnimalMoveToPoint : MonoBehaviour
{
    void Start()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        // This is where the chicken will walk to
        Vector3 targetPosition = transform.position + new Vector3(3, 0, 3);

        agent.SetDestination(targetPosition);
    }
}
