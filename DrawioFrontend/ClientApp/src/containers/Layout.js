import React, {useState} from 'react';
import MainMenu from './MainMenu';
import Game from './Game';
import { useHistory } from "react-router-dom";
import { Switch, Route } from 'react-router-dom';
import classes from './css/Layout.module.css';
import Footer from '../components/Footer';


const Layout = () => {
  const [gameState, setGameState] = useState(null);
  const history = useHistory();



  const onGameStarting = (gamestate) => {
      setGameState(gamestate);
      history.push('/game');
  };

  return (
    <>
      <div className={classes.Layout}>
        <Switch>
          <Route exact path='/game' render={() => <Game gameState={gameState}/>} />
          <Route path='/' render={() => <MainMenu gameStarting={onGameStarting}/>} />
        </Switch>
      </div>
      <Footer />
    </>



  );
};

export default Layout;
