using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Sharper.C.Testing.Laws;

namespace Sharper.C.Tests
{

using Fuchu;
using static Control.StepModule;
using static Data.ChainModule;
using static Testing.PropertyModule;
using static Test.SystemArbitraryModule;

public static class StackoverflowTests
{
    public static IEnumerable<int> Nats
    =>
        IterateChain(0, n => n + 1).Eval();

    public static int NthNat(int n)
    =>
        Nats.Skip(n).First();

    public static void DoesNotOverflow()
    {
        Console.WriteLine(NthNat(100));
        Console.WriteLine(NthNat(1000));
        Console.WriteLine(NthNat(10000));
        Console.WriteLine(NthNat(100000));
        Console.WriteLine(NthNat(1000000));
        Console.WriteLine(NthNat(10000000));
    }
}

public static class ChainTests
{
    [Tests]
    public static Test Tests
    =>
        nameof(Chain<int>)
        .Group
          ( IsMonad(AnyInt)
          , "Iterates".WithoutOverflow(() => IterateChain(0, n => n + 1))
          );

    public static Test IsMonad<A>(Arbitrary<A> arbA)
      where A : IEquatable<A>
    =>
        MonadLaws.For
          ( LastLink
          , f => fa => fa.Map(f)
          , f => fa => fa.FlatMap(f)
          , (s1, s2) => s1.Eval().SequenceEqual(s1.Eval())
          , AnyChain(arbA)
          , AnyFunc1<A, Chain<A>>(AnyChain(arbA))
          , AnyFunc1<A, A>(arbA)
          , arbA
          );

    public static Test WithoutOverflow<A>(this string label, Func<Chain<A>> f)
    =>
        label.Group
          ( Test.Case("Build computation", () => f())
          , Test.Case("Evaluate computation", () => f().Eval().Skip(1000000))
          );

    public static Arbitrary<Chain<A>> AnyChain<A>(Arbitrary<A> arbA)
    =>
        AnySeq(arbA).Convert
          ( xs => xs.Aggregate(EndChain<A>(), (s, a) => YieldLink(a, s))
          , s => s.Eval()
          );

    // public static Gen<Chain<A>> Sequence<A>(Chain<Gen<A>> xs)
    // =>
    //     xs.Eval().FoldRight
    //       ( Gen.Constant(EndChain<A>())
    //       , (ga, x) =>
    //             x.Map
    //               ( gsa =>
    //                     from sa in gsa
    //                     from a in ga
    //                     select YieldLink(a, sa)
    //               )
    //       )
    //     .Eval();

    // public static Gen<IEnumerable<A>> Sequence<A>(IEnumerable<Gen<A>> xs)
    // =>
    //     Sequence(xs.ToChain()).Select(sa => sa.Eval());
}

public static class StepTests
{
    [Tests]
    public static Test Tests
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

    public static Arbitrary<Step<A>> AnyStep<A>(Arbitrary<A> arbA)
    =>
        arbA.Convert(Done, s => s.Eval());

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

    public static Test WithoutOverflow<A>(this string label, Func<Step<A>> f)
    =>
        label.Group
          ( Test.Case("Build computation", () => f())
          , Test.Case("Evaluate computation", () => f().Eval())
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

public sealed class Program
{
    public static Test[] Tests
    =>
        new[]
        { StepTests.Tests
        , ChainTests.Tests
        };

    public int Main(string[] args)
    =>
        Tests.Run();
}

}
