import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { TopBar } from './TopBar';

export function Layout() {
  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <div className="mr-72">
        <TopBar />
        <main className="px-8 py-6 animate-fade-in">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
