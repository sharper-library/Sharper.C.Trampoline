using System;

namespace Sharper.C.Control
{

public static partial class StepModule
{
    public const uint DefaultRecurMaxFrames = 1000;

    public static Step<A> Delay<A>(Func<A> f)
    =>
        Suspend(() => Done(f()));

    public static Step<A> Done<A>(A a)
    =>
        new DoneCase<A>(a);

    public static Step<A> Suspend<A>(Func<Step<A>> k)
    =>
        new SuspendCase<A>(k);

    public static Step<A> Defer<A>(Step<A> x)
    =>
        new LazyCase<A>(x);

    public static LazyStep<A> Thunk<A>(Step<A> x)
    =>
        new LazyStep<A>(x);

    public static Func<A, Step<B>> Fix<A, B>
      ( Func<Func<A, Step<B>>, Func<A, Step<B>>> f
      , uint max = DefaultRecurMaxFrames
      )
    =>
        Fix(f, max, max);

    public static Func<A, B> Recur<A, B>
      ( Func<Func<A, Step<B>>, Func<A, Step<B>>> f
      , uint max = DefaultRecurMaxFrames
      )
    =>
        a => Fix(f, max, max)(a).Eval();

    public static Step<B> Select<A, B>
      ( this Step<A> ta
      , Func<A, B> f
      )
    =>
        ta.Map(f);

    public static Step<C> SelectMany<A, B, C>
      ( this Step<A> ta
      , Func<A, Step<B>> f
      , Func<A, B, C> g
      )
    =>
        ta.FlatMap(a => f(a).Map(b => g(a, b)));

    private static Func<A, Step<B>> Fix<A, B>
      ( Func<Func<A, Step<B>>, Func<A, Step<B>>> f
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

    public abstract class Step<A>
    {
        internal Step()
        {
        }

        public abstract Step<B> FlatMap<B>(Func<A, Step<B>> f);
        public abstract Step<A> Next { get; }
        internal abstract Step<B> FlatMapNext<B>(Func<A, Step<B>> k);

        public A Eval()
        {
            var t = Next;
            for (; !t.IsDone; t = t.Next)
            {
            }
            return ((DoneCase<A>)t).Value;
        }

        public Step<B> Map<B>(Func<A, B> f)
        =>
            FlatMap(a => Done(f(a)));

        public bool IsDone
        =>
            this is DoneCase<A>;
    }

    private sealed class FlatMapCase<A, B>
      : Step<B>
    {
        public Step<A> Sub { get; }
        public Func<A, Step<B>> Cont { get; }

        public FlatMapCase(Step<A> sub, Func<A, Step<B>> cont)
        {
            Sub = sub;
            Cont = cont;
        }

        public override Step<C> FlatMap<C>(Func<B, Step<C>> f)
        =>
            new FlatMapCase<A, C>(Sub, a => Suspend(() => Cont(a)).FlatMap(f));

        public override Step<B> Next
        =>
            Sub.FlatMapNext(Cont);

        internal override Step<C> FlatMapNext<C>(Func<B, Step<C>> k)
        =>
            Sub.FlatMap(a => Cont(a).FlatMap(k)).Next;
    }

    private sealed class DoneCase<A>
      : Step<A>
    {
        public A Value { get; }

        public DoneCase(A a)
        {
            Value = a;
        }

        public override Step<B> FlatMap<B>(Func<A, Step<B>> f)
        =>
            new FlatMapCase<A, B>(this, f);

        public override Step<A> Next
        =>
            this;

        internal override Step<B> FlatMapNext<B>(Func<A, Step<B>> k)
        =>
            k(Value);
    }

    private sealed class SuspendCase<A>
      : Step<A>
    {
        public Func<Step<A>> Cont { get; }

        public SuspendCase(Func<Step<A>> cont)
        {
            Cont = cont;
        }

        public override Step<B> FlatMap<B>(Func<A, Step<B>> f)
        =>
            new FlatMapCase<A, B>(this, f);

        public override Step<A> Next
        =>
            Cont();

        internal override Step<B> FlatMapNext<B>(Func<A, Step<B>> k)
        =>
            Cont().FlatMap(k);
    }

    private sealed class LazyCase<A>
      : Step<A>
    {
        // TODO make this a thread-safe Box<Step<A>>
        public Step<A> Comp { get; private set; }

        public LazyCase(Step<A> comp)
        {
            Comp = comp;
        }

        public override Step<B> FlatMap<B>(Func<A, Step<B>> f)
        =>
            new FlatMapCase<A, B>(Next, f);

        public override Step<A> Next
        {
            get
            {
                var a = Comp.Eval();
                Comp = Done(a);
                return Comp;
            }
        }

        internal override Step<B> FlatMapNext<B>(Func<A, Step<B>> k)
        =>
            Next.FlatMap(k);
    }

    public sealed class LazyStep<A>
    {
        private Step<A> comp;

        internal LazyStep(Step<A> comp)
        {
            this.comp = comp;
        }

        public Step<A> Step
        =>
            comp is DoneCase<A>
            ? comp
            : comp = Done(comp.Eval());
    }
}

}
