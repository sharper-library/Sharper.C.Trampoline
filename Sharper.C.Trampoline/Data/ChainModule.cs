using System;
using System.Collections.Generic;

namespace Sharper.C.Data
{

using static Control.StepModule;

public static class ChainModule
{
    public static Chain<A> EndChain<A>()
    =>
        new DoneCase<A>();

    public static Chain<A> LastLink<A>(A a)
    =>
        new YieldCase<A>(a, new DoneCase<A>());

    public static Chain<A> SuspendChain<A>(Func<Chain<A>> k)
    =>
        new SuspendCase<A>(k);

    public static Chain<A> YieldLink<A>(A a, Chain<A> s)
    =>
        new YieldCase<A>(a, s);

    public static Chain<A> IterateChain<A>(A a, Func<A, A> f)
    =>
        SuspendChain(() => YieldLink(a, IterateChain(f(a), f)));

    public static Chain<A> ToChain<A>(this IEnumerable<A> xs)
    =>
        EnumerateStep(xs.GetEnumerator());

    private static Func<A, Chain<B>> Fix<A, B>
      ( Func<Func<A, Chain<B>>, Func<A, Chain<B>>> f
      , uint n
      , uint max
      )
    =>
        x =>
            f
              ( (n == 0
                ? a => SuspendChain<B>(() => Fix(f, max, max)(a))
                : Fix(f, n - 1, max))
              )
              (x);

    private static Chain<A> EnumerateStep<A>(IEnumerator<A> e)
    =>
        e.MoveNext()
        ? YieldLink(e.Current, EnumerateStep(e))
        : EndChain<A>();

    public abstract class Chain<A>
    {
        internal Chain()
        {
        }

        public abstract Chain<A> Next { get; }
        internal abstract Chain<B> FlatMapNext<B>(Func<A, Chain<B>> k);

        public B Match<B>
          ( Func<B> done
          , Func<Func<Chain<A>>, B> suspend
          , Func<A, Chain<A>, B> @yield
          , Func<Chain<A>, Chain<A>, B> concat
          )
        =>
            this is DoneCase<A>
            ? done()

            : this is SuspendCase<A>
            ? suspend(((SuspendCase<A>)this).Cont)

            : this is YieldCase<A>
            ? @yield(((YieldCase<A>)this).Value, ((YieldCase<A>)this).Next)

            : concat(((ConcatCase<A>)this).Begin, ((ConcatCase<A>)this).End);

        public B Match<B>
          ( Func<B> done
          , Func<A, Chain<A>, B> @yield
          )
        =>
            this is DoneCase<A>
            ? done()

            : this is YieldCase<A>
            ? @yield(((YieldCase<A>)this).Value, ((YieldCase<A>)this).Next)

            : NextYieldOrEnd().Match(done, @yield);

        public Step<B> FoldRight<B>
          ( B x
          , Func<A, Step<B>, Step<B>> f
          )
        =>
            Suspend
              ( () =>
                    Match
                      ( () => Done(x)
                      , (a, @as) => f(a, @as.FoldRight(x, f))
                      )
              );

        public Chain<B> Map<B>(Func<A, B> f)
        =>
            Match
              ( EndChain<B>
              , k => SuspendChain(() => k().Map(f))
              , (a, s) => YieldLink(f(a), s.Map(f))
              , (y, z) => new ConcatCase<B>(y.Map(f), z.Map(f))
              );

        public virtual Chain<B> FlatMap<B>(Func<A, Chain<B>> k)
        =>
            new FlatMapCase<A, B>(this, k);

        public IEnumerable<A> Eval()
        {
            var x = this;
            for (; !(x is DoneCase<A>); x = x.Next)
            {
                if (x is YieldCase<A>)
                {
                    yield return ((YieldCase<A>)x).Value;
                }
            }
        }

        private Chain<A> NextYieldOrEnd()
        {
            var x = this;
            for (; !(x is DoneCase<A>) && !(x is YieldCase<A>); x = x.Next)
            {
            }
            return x;
        }
    }

    private sealed class DoneCase<A>
      : Chain<A>
    {
        public DoneCase()
        {
        }

        public override Chain<A> Next
        =>
            this;

        internal override Chain<B> FlatMapNext<B>(Func<A, Chain<B>> _)
        =>
            new DoneCase<B>();
    }

    private sealed class SuspendCase<A>
      : Chain<A>
    {
        public Func<Chain<A>> Cont { get; }

        public SuspendCase(Func<Chain<A>> cont)
        {
            Cont = cont;
        }

        public override Chain<A> Next
        =>
            Cont();

        internal override Chain<B> FlatMapNext<B>(Func<A, Chain<B>> k)
        =>
            Cont().FlatMap(k);
    }

    private sealed class YieldCase<A>
      : Chain<A>
    {
        public A Value { get; }
        public override Chain<A> Next { get; }

        public YieldCase(A value, Chain<A> next)
        {
            Value = value;
            Next = next;
        }

        internal override Chain<B> FlatMapNext<B>(Func<A, Chain<B>> k)
        =>
            new ConcatCase<B>(k(Value), Next.FlatMap(k));
    }

    private sealed class FlatMapCase<A, B>
      : Chain<B>
    {
        public Chain<A> Sub { get; }
        public Func<A, Chain<B>> Cont { get; }

        public FlatMapCase(Chain<A> sub, Func<A, Chain<B>> cont)
        {
            Sub = sub;
            Cont = cont;
        }

        public override Chain<B> Next
        =>
            Sub.FlatMapNext(Cont);

        public override Chain<C> FlatMap<C>(Func<B, Chain<C>> f)
        =>
            new FlatMapCase<A, C>(Sub, a => SuspendChain(() => Cont(a)).FlatMap(f));

        internal override Chain<C> FlatMapNext<C>(Func<B, Chain<C>> k)
        =>
            Sub.FlatMap(a => Cont(a).FlatMap(k)).Next;
    }

    private sealed class ConcatCase<A>
      : Chain<A>
    {
        public Chain<A> Begin { get; }
        public Chain<A> End { get; }

        public ConcatCase(Chain<A> begin, Chain<A> end)
        {
            Begin = begin;
            End = end;
        }

        public override Chain<A> Next
        =>
            Begin is DoneCase<A>
            ? End
            : new ConcatCase<A>(Begin.Next, End);

        internal override Chain<B> FlatMapNext<B>(Func<A, Chain<B>> k)
        =>
            new ConcatCase<B>(Begin.FlatMapNext(k), End.FlatMapNext(k));
    }
}

}
