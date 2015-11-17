using System;
using System.Linq;
using FsCheck;
using Fuchu;

namespace Sharper.C.Tests
{

using Testing.Laws;
using static Control.StepModule;
using static Testing.PropertyModule;
using static Testing.StepTestingModule;
using static Testing.SystemArbitraryModule;

public static class StepTestsModule
{
    [Tests]
    public static Test StepTests
    =>
        nameof(Step<int>)
        .Group
          ( IsMonad(AnyInt)

          , "Iterate over Suspend"
            .WithoutOverflow(() => IterateSuspend())

          , "Iterate over right associated FlatMap"
            .WithoutOverflow(() => IterateRightAssociatedFlatMap())

          , "Iterate over left associated FlatMap"
            .WithoutOverflow(() => IterateLeftAssociatedFlatMap())

          , "Iterate over function fixed point"
            .WithoutOverflow
              ( () =>
                    Fix<int, int>
                      ( recur => n => n == 0 ? Done(n) : recur(n - 1)
                      )
                      (1000000)
              )
          );

    public static Test IsMonad<A>(Arbitrary<A> arbA)
      where A : IEquatable<A>
    =>
        MonadLaws.For
          ( Done
          , f => fa => fa.Map(f)
          , f => fa => fa.FlatMap(f)
          , (s1, s2) => s1.Eval().Equals(s1.Eval())
          , AnyStep(arbA)
          , AnyFunc1<A, Step<A>>(AnyStep(arbA))
          , AnyFunc1<A, A>(arbA)
          , arbA
          );

    private static Step<int> IterateSuspend(int target = 1000000, int n = 0)
    =>
        n == target
        ? Done(n)
        : Suspend(() => IterateSuspend(target, n + 1));

    private static Step<int> IterateRightAssociatedFlatMap
      ( int target = 1000000
      )
    =>
        Enumerable
        .Repeat(Done(1), target)
        .Aggregate((b, a) => a.FlatMap(_ => b));

    private static Step<int> IterateLeftAssociatedFlatMap
      ( int target = 1000000
      )
    =>
        Enumerable
        .Repeat(Done(1), target)
        .Aggregate((b, a) => b.FlatMap(_ => a));
}

}
