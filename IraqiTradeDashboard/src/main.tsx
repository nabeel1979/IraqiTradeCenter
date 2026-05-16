import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'sonner';
import App from './App';
import './globals.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, refetchOnWindowFocus: false, retry: 1 },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
        <Toaster
          position="top-left"
          theme="dark"
          richColors
          closeButton
          toastOptions={{
            style: {
              fontFamily: '"IBM Plex Sans Arabic", sans-serif',
              direction: 'rtl',
            },
          }}
        />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>
);
