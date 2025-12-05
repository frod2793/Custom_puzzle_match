using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace GravitySpinMatch.Core
{
    public interface IMovable
    {
        UniTask MoveToAsync(Vector3 targetPosition, float duration, CancellationToken token);
    }
}
