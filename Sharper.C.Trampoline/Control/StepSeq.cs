using System;
using System.Collections.Generic;

namespace Sharper.C.Control
{

public static class StepSeqModule
{
    public static StepSeq<A> End<A>()
    =>
        new DoneCase<A>();

    public static StepSeq<A> End<A>(A a)
    =>
        new YieldCase<A>(a, new DoneCase<A>());

    public static StepSeq<A> Suspend<A>(Func<StepSeq<A>> k)
    =>
        new SuspendCase<A>(k);

    public static StepSeq<A> Yield<A>(A a, StepSeq<A> s)
    =>
        new YieldCase<A>(a, s);

    public static IEnumerable<A> Eval<A>(this StepSeq<A> ss)
    {
        var x = ss;
        for (; !(x is DoneCase<A>); x = x.Next)
        {
            if (x is YieldCase<A>)
            {
                yield return ((YieldCase<A>)x).Value;
            }
        }
    }

    public static StepSeq<A> Iterate<A>(A a, Func<A, A> f)
    =>
        Suspend(() => Yield(a, Iterate(f(a), f)));

    private static Func<A, StepSeq<B>> Fix<A, B>
      ( Func<Func<A, StepSeq<B>>, Func<A, StepSeq<B>>> f
      , uint n
      , uint max
      )
    =>
        x =>
            f
              ( n == 0
                ? a => Suspend(() => Fix(f, max, max)(a))
                : Fix(f, n - 1, max)
              )
              (x);

    public abstract class StepSeq<A>
    {
        internal StepSeq()
        {
        }

        public abstract StepSeq<A> Next { get; }
        internal abstract StepSeq<B> FlatMapNext<B>(Func<A, StepSeq<B>> k);

        public B Match<B>
          ( Func<B> done
          , Func<Func<StepSeq<A>>, B> suspend
          , Func<A, StepSeq<A>, B> @yield
          , Func<StepSeq<A>, StepSeq<A>, B> concat
          )
        =>
            this is DoneCase<A>
            ? done()

            : this is SuspendCase<A>
            ? suspend(((SuspendCase<A>)this).Cont)

            : this is YieldCase<A>
            ? @yield(((YieldCase<A>)this).Value, ((YieldCase<A>)this).Next)

            : concat(((ConcatCase<A>)this).Begin, ((ConcatCase<A>)this).End);


        public StepSeq<B> Map<B>(Func<A, B> f)
        =>
            Match
              ( End<B>
              , k => Suspend(() => k().Map(f))
              , (a, s) => Yield(f(a), s.Map(f))
              , (y, z) => new ConcatCase<B>(y.Map(f), z.Map(f))
              );

        public virtual StepSeq<B> FlatMap<B>(Func<A, StepSeq<B>> k)
        =>
            new FlatMapCase<A, B>(this, k);
    }

    private sealed class DoneCase<A>
      : StepSeq<A>
    {
        public DoneCase()
        {
        }

        public override StepSeq<A> Next
        =>
            this;

        internal override StepSeq<B> FlatMapNext<B>(Func<A, StepSeq<B>> _)
        =>
            new DoneCase<B>();
    }

    private sealed class SuspendCase<A>
      : StepSeq<A>
    {
        public Func<StepSeq<A>> Cont { get; }

        public SuspendCase(Func<StepSeq<A>> cont)
        {
            Cont = cont;
        }

        public override StepSeq<A> Next
        =>
            Cont();

        internal override StepSeq<B> FlatMapNext<B>(Func<A, StepSeq<B>> k)
        =>
            Cont().FlatMap(k);
    }

    private sealed class YieldCase<A>
      : StepSeq<A>
    {
        public A Value { get; }
        public override StepSeq<A> Next { get; }

        public YieldCase(A value, StepSeq<A> next)
        {
            Value = value;
            Next = next;
        }

        internal override StepSeq<B> FlatMapNext<B>(Func<A, StepSeq<B>> k)
        =>
            new ConcatCase<B>(k(Value), Next.FlatMap(k));
    }

    private sealed class FlatMapCase<A, B>
      : StepSeq<B>
    {
        public StepSeq<A> Sub { get; }
        public Func<A, StepSeq<B>> Cont { get; }

        public FlatMapCase(StepSeq<A> sub, Func<A, StepSeq<B>> cont)
        {
            Sub = sub;
            Cont = cont;
        }

        public override StepSeq<B> Next
        =>
            Sub.FlatMapNext(Cont);

        public override StepSeq<C> FlatMap<C>(Func<B, StepSeq<C>> f)
        =>
            new FlatMapCase<A, C>(Sub, a => Suspend(() => Cont(a)).FlatMap(f));

        internal override StepSeq<C> FlatMapNext<C>(Func<B, StepSeq<C>> k)
        =>
            Sub.FlatMap(a => Cont(a).FlatMap(k)).Next;
    }

    private sealed class ConcatCase<A>
      : StepSeq<A>
    {
        public StepSeq<A> Begin { get; }
        public StepSeq<A> End { get; }

        public ConcatCase(StepSeq<A> begin, StepSeq<A> end)
        {
            Begin = begin;
            End = end;
        }

        public override StepSeq<A> Next
        =>
            Begin is DoneCase<A>
            ? End
            : new ConcatCase<A>(Begin.Next, End);

        internal override StepSeq<B> FlatMapNext<B>(Func<A, StepSeq<B>> k)
        =>
            new ConcatCase<B>(Begin.FlatMapNext(k), End.FlatMapNext(k));
    }
}

}
