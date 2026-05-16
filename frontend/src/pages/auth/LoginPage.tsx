import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { Phone, Lock, ArrowLeft, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { authApi } from '@/lib/api/auth';
import { setToken } from '@/lib/api/client';
import { useAuthStore } from '@/lib/auth/auth-store';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const setUser = useAuthStore(s => s.setUser);
  const [phone, setPhone] = useState('07700000000');
  const [password, setPassword] = useState('');

  const loginMutation = useMutation({
    mutationFn: () => authApi.login({ phone, password }),
    onSuccess: res => {
      if (res.success && res.data) {
        setToken(res.data.token);
        setUser(res.data.user);
        toast.success(`أهلاً ${res.data.user.fullName}`);
        const from = (location.state as any)?.from?.pathname ?? '/';
        navigate(from, { replace: true });
      }
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!phone.match(/^07[0-9]{9}$/)) {
      toast.error('رقم الهاتف يجب أن يبدأ بـ 07 ويتكون من 11 رقماً');
      return;
    }
    if (password.length < 4) {
      toast.error('كلمة المرور قصيرة جداً');
      return;
    }
    loginMutation.mutate();
  };

  return (
    <div className="relative min-h-screen overflow-hidden bg-background">
      {/* Decorative background */}
      <div className="absolute inset-0">
        <div className="absolute right-[-10%] top-[-10%] h-[500px] w-[500px] rounded-full bg-primary/15 blur-[120px]" />
        <div className="absolute bottom-[-20%] left-[-10%] h-[600px] w-[600px] rounded-full bg-primary/10 blur-[140px]" />
        <div className="pattern-meso absolute inset-0 opacity-30" />
      </div>

      <div className="relative flex min-h-screen items-center justify-center px-4 py-12">
        <div className="w-full max-w-[440px]">
          {/* Brand */}
          <div className="mb-10 text-center">
            <div className="mb-5 inline-flex">
              <div className="relative">
                <div className="absolute inset-0 rounded-full bg-primary/40 blur-xl" />
                <div className="relative flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-primary via-primary to-primary/60 shadow-2xl shadow-primary/30">
                  <svg viewBox="0 0 24 24" className="h-7 w-7 text-primary-foreground" fill="currentColor">
                    <path d="M12 2l2.4 7.2H22l-6.2 4.4L18.4 22 12 17.4 5.6 22l2.6-8.4L2 9.2h7.6z" />
                  </svg>
                </div>
              </div>
            </div>
            <h1 className="font-display text-3xl font-semibold tracking-tight">
              مركز التجارة العراقي
            </h1>
            <p className="mt-1.5 text-[11px] uppercase tracking-[0.22em] text-primary/70">
              Iraqi Trade Center · Wholesale Dashboard
            </p>
            <div className="mx-auto mt-4 h-px w-12 bg-gradient-to-r from-transparent via-primary to-transparent" />
          </div>

          {/* Card */}
          <div className="rounded-xl border border-border/60 bg-card/40 p-8 backdrop-blur-xl shadow-2xl shadow-black/20 animate-slide-up">
            <div className="mb-6">
              <h2 className="font-display text-xl font-medium">أهلاً بعودتك</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                سجّل دخول لإدارة شركتك
              </p>
            </div>

            <form onSubmit={handleSubmit} className="space-y-5">
              <div className="space-y-2">
                <Label htmlFor="phone">رقم الهاتف</Label>
                <div className="relative">
                  <Phone className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="phone"
                    type="tel"
                    dir="ltr"
                    placeholder="07XXXXXXXXX"
                    value={phone}
                    onChange={e => setPhone(e.target.value)}
                    className="pr-10 text-left font-mono"
                    autoComplete="tel"
                    disabled={loginMutation.isPending}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label htmlFor="password">كلمة المرور</Label>
                  <button type="button" className="text-xs text-primary/80 hover:text-primary">
                    نسيت كلمة المرور؟
                  </button>
                </div>
                <div className="relative">
                  <Lock className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="password"
                    type="password"
                    placeholder="••••••••"
                    value={password}
                    onChange={e => setPassword(e.target.value)}
                    className="pr-10"
                    autoComplete="current-password"
                    disabled={loginMutation.isPending}
                  />
                </div>
              </div>

              <Button
                type="submit"
                size="lg"
                className="w-full font-medium glow-primary"
                disabled={loginMutation.isPending}
              >
                {loginMutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    جاري التحقق...
                  </>
                ) : (
                  <>
                    <ArrowLeft className="h-4 w-4" />
                    دخول
                  </>
                )}
              </Button>
            </form>

            <div className="mt-6 flex items-center gap-3 text-xs text-muted-foreground">
              <div className="h-px flex-1 bg-border" />
              <span>محمي بـ JWT + bcrypt</span>
              <div className="h-px flex-1 bg-border" />
            </div>
          </div>

          {/* Footer */}
          <p className="mt-6 text-center text-xs text-muted-foreground">
            بحاجة لحساب جديد؟ تواصل مع مدير المنصة
          </p>
        </div>
      </div>
    </div>
  );
}
